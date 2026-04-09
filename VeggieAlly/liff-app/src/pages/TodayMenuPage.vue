<template>
  <div class="menu-page">
    <!-- 載入中 -->
    <div v-if="loading && !menuData" class="loading">
      <p>載入菜單中...</p>
    </div>

    <!-- 空狀態：菜單未發布 -->
    <div v-else-if="!menuData" class="empty-state">
      <h2>📋 今日菜單</h2>
      <div class="empty-content">
        <p>📅 今日菜單尚未發布</p>
        <p class="empty-subtitle">請稍後再查看</p>
        <button @click="loadMenu" class="retry-btn">
          重新載入
        </button>
      </div>
    </div>

    <!-- 菜單內容 -->
    <div v-else class="menu-container">
      <!-- Header -->
      <header class="header">
        <h1 class="title">🥬 今日菜單</h1>
        <div class="menu-info">
          <p class="date">{{ formatDate(menuData.date) }}</p>
          <p class="publisher">發布者：商家</p>
          <p class="item-count">共 {{ menuData.items.length }} 個品項</p>
        </div>
      </header>

      <!-- 品項列表 -->
      <div class="items-container">
        <MenuItem
          v-for="item in orderItems"
          :key="item.id"
          :item="item"
          @qty-change="handleQtyChange"
        />
      </div>

      <!-- 底部合計與按鈕 -->
      <footer class="footer">
        <div class="summary">
          <span class="total-info">
            合計：{{ totalItems }} 項　${{ totalAmount }}
          </span>
        </div>
        <button 
          class="order-btn"
          :disabled="!canOrder || submitting"
          @click="handleSubmitOrder"
        >
          {{ submitting ? '處理中...' : '🛒 送出訂單' }}
        </button>
      </footer>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, inject, onMounted } from 'vue'
import type { 
  TodayMenuResponse, 
  OrderItem, 
  DeductInventoryRequest,
  DeductInventoryResponse 
} from '@/types/api'
import { useApi } from '@/composables/useApi'
import type { LiffService } from '@/composables/useLiff'
import MenuItem from '@/components/MenuItem.vue'
import { showToast } from '@/components/Toast.vue'

const liff = inject<LiffService>('liff')!
const api = useApi(() => liff.getToken())

const loading = ref(false)
const submitting = ref(false)
const menuData = ref<TodayMenuResponse | null>(null)
const orderItems = ref<OrderItem[]>([])

// 計算屬性
const totalItems = computed(() => {
  return orderItems.value.reduce((sum: number, item: OrderItem) => sum + item.orderQty, 0)
})

const totalAmount = computed(() => {
  return orderItems.value.reduce((sum: number, item: OrderItem) => sum + (item.sell_price * item.orderQty), 0)
})

const canOrder = computed(() => {
  return totalItems.value > 0
})

// 格式化日期
function formatDate(dateStr: string): string {
  const date = new Date(dateStr)
  return date.toLocaleDateString('zh-TW', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    weekday: 'short'
  })
}

// 載入菜單
async function loadMenu(): Promise<void> {
  loading.value = true
  
  try {
    // 從環境變數或 URL query string 取得 tenant_id
    const tenantId = import.meta.env.VITE_TENANT_ID
      || new URLSearchParams(window.location.search).get('tenantId')
      || ''

    if (!tenantId) {
      console.warn('[VeggieAlly] VITE_TENANT_ID not set and tenantId not in URL')
    }
    
    const response = await api.get<TodayMenuResponse>('/api/menu/today', {
      tenant_id: tenantId
    })
    
    menuData.value = response
    
    // 轉換為訂單品項格式（保留 is_new 與 original_qty 給 MenuItem 顯示）
    orderItems.value = response.items.map(item => ({
      id: item.id,
      name: item.name,
      is_new: item.is_new,
      sell_price: item.sell_price,
      original_qty: item.original_qty,
      remaining_qty: item.remaining_qty,
      unit: item.unit,
      orderQty: 0
    }))
    
  } catch (error: any) {
    console.error('載入菜單失敗:', error)
    
    if (error?.error?.code === 'NOT_FOUND' || error?.error?.message?.includes('404')) {
      // 菜單未發布，保持 menuData 為 null 顯示空狀態
      menuData.value = null
    } else if (error?.error?.code === 'UNAUTHORIZED') {
      showToast('登入已過期，請重新開啟', 'error')
      setTimeout(() => liff.closeWindow(), 1500)
    } else {
      const message = error?.error?.message || '載入菜單失敗'
      showToast(`❌ ${message}`, 'error')
    }
  } finally {
    loading.value = false
  }
}

// 處理數量變更
function handleQtyChange(itemId: string, newQty: number): void {
  const item = orderItems.value.find((i: OrderItem) => i.id === itemId)
  if (item) {
    item.orderQty = Math.max(0, Math.min(newQty, item.remaining_qty))
  }
}

// 送出訂單 (all-or-nothing 模式)
async function handleSubmitOrder(): Promise<void> {
  if (!canOrder.value || submitting.value || !menuData.value) return
  
  submitting.value = true
  
  try {
    // 篩選出需要下單的品項 (orderQty > 0)
    const itemsToOrder = orderItems.value.filter((item: OrderItem) => item.orderQty > 0)
    
    if (itemsToOrder.length === 0) {
      showToast('請選擇品項數量', 'warning')
      return
    }
    
    // All-or-nothing: 序列呼叫但記錄成功品項，失敗時提供明確反饋
    const results: DeductInventoryResponse[] = []
    const successItems: string[] = []
    let failureReason: string = ''
    
    for (const item of itemsToOrder) {
      try {
        const request: DeductInventoryRequest = {
          item_id: item.id,
          amount: item.orderQty
        }
        
        const response = await api.patch<DeductInventoryResponse>(
          '/api/menu/inventory',
          request,
          { 'X-Tenant-Id': menuData.value.tenant_id }
        )
        
        results.push(response)
        successItems.push(item.name)
        
      } catch (error: any) {
        console.error(`品項 ${item.name} 下單失敗:`, error)
        
        if (error?.error?.code === 'CONFLICT' || error?.error?.message?.includes('409')) {
          failureReason = `${item.name} 庫存不足`
        } else {
          const message = error?.error?.message || '系統錯誤'
          failureReason = `${item.name} ${message}`
        }
        break // 中止剩餘品項的下單
      }
    }
    
    if (results.length === itemsToOrder.length) {
      // 全部成功
      showToast(`✅ 訂單提交成功！共 ${totalItems.value} 項`, 'success')
      
      // 重新載入菜單以更新庫存
      setTimeout(() => {
        loadMenu()
      }, 1000)
    } else {
      // 部分成功 - 顯示明確狀態並要求重新整理
      if (successItems.length > 0) {
        showToast(
          `⚠️ 部分品項訂購失敗：${failureReason}\n已成功：${successItems.join(', ')}\n請重新整理後重試剩餘品項`, 
          'warning'
        )
      } else {
        showToast(`❌ ${failureReason}`, 'error')
      }
      
      // 自動重新載入最新庫存狀態，讓用戶看到真實狀況
      setTimeout(() => {
        loadMenu()
      }, 2000)
    }
    
  } catch (error: any) {
    console.error('送出訂單失敗:', error)
    const message = error?.error?.message || '送出訂單失敗，請稍後再試'
    showToast(`❌ ${message}`, 'error')
  } finally {
    submitting.value = false
  }
}

// 初始化
onMounted(() => {
  loadMenu()
})
</script>

<style scoped>
.menu-page {
  min-height: 100vh;
  background-color: var(--bg-color);
  display: flex;
  flex-direction: column;
}

.loading {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.1rem;
  color: var(--text-secondary);
}

.empty-state {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: var(--spacing-xl);
  text-align: center;
}

.empty-state h2 {
  font-size: 1.8rem;
  margin-bottom: var(--spacing-lg);
  color: var(--text-primary);
}

.empty-content p {
  font-size: 1.1rem;
  color: var(--text-secondary);
  margin-bottom: var(--spacing-sm);
}

.empty-subtitle {
  font-size: 0.9rem !important;
  color: var(--text-tertiary) !important;
}

.retry-btn {
  margin-top: var(--spacing-lg);
  padding: var(--spacing-sm) var(--spacing-lg);
  background-color: var(--primary-color);
  color: white;
  border: none;
  border-radius: var(--radius-md);
  cursor: pointer;
  font-size: 1rem;
}

.retry-btn:hover {
  background-color: var(--primary-hover);
}

.menu-container {
  flex: 1;
  display: flex;
  flex-direction: column;
  max-width: 500px;
  margin: 0 auto;
  width: 100%;
}

.header {
  padding: var(--spacing-md);
  background-color: var(--card-bg);
  border-bottom: 1px solid var(--border-color);
}

.title {
  font-size: 1.6rem;
  font-weight: bold;
  text-align: center;
  margin-bottom: var(--spacing-sm);
  color: var(--text-primary);
}

.menu-info {
  text-align: center;
}

.date {
  font-size: 1.1rem;
  font-weight: 600;
  color: var(--primary-color);
  margin-bottom: var(--spacing-xs);
}

.publisher,
.item-count {
  font-size: 0.9rem;
  color: var(--text-secondary);
  margin-bottom: var(--spacing-xs);
}

.items-container {
  flex: 1;
  padding: var(--spacing-sm);
  overflow-y: auto;
  max-height: calc(100vh - 200px);
}

.footer {
  background-color: var(--card-bg);
  border-top: 1px solid var(--border-color);
  padding: var(--spacing-md);
  position: sticky;
  bottom: 0;
}

.summary {
  text-align: center;
  margin-bottom: var(--spacing-sm);
}

.total-info {
  font-size: 1.1rem;
  font-weight: 600;
  color: var(--text-primary);
}

.order-btn {
  width: 100%;
  padding: var(--spacing-md);
  background-color: var(--primary-color);
  color: white;
  border: none;
  border-radius: var(--radius-md);
  font-size: 1.1rem;
  font-weight: 600;
  cursor: pointer;
  transition: background-color 0.2s;
}

.order-btn:hover:not(:disabled) {
  background-color: var(--primary-hover);
}

.order-btn:disabled {
  background-color: var(--disabled-color);
  cursor: not-allowed;
}
</style>