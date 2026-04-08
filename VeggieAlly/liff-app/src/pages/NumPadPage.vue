<template>
  <div class="numpad-page">
    <!-- 錯誤頁面 -->
    <ErrorPage v-if="validationError" :title="validationError" />
    
    <!-- 正常頁面 -->
    <div v-else class="numpad-container">
      <!-- Header -->
      <header class="header">
        <h1 class="title">🔧 修正價格</h1>
        <div class="item-info">
          <p class="item-name">品名：{{ itemInfo.name }}</p>
          <p class="field-name">欄位：{{ fieldDisplayName }}</p>
          <p class="current-price">目前：${{ itemInfo.currentPrice }}</p>
        </div>
      </header>

      <!-- 價格顯示區 -->
      <div class="price-display">
        <span class="currency">$</span>
        <span class="amount">{{ displayValue }}</span>
      </div>

      <!-- 數字鍵盤 -->
      <NumPad 
        :value="inputValue"
        @input="handleInput"
        @backspace="handleBackspace"
      />

      <!-- 操作按鈕 -->
      <div class="actions">
        <button 
          class="confirm-btn"
          :disabled="!canSubmit || submitting"
          @click="handleSubmit"
        >
          {{ submitting ? '送出中...' : '✅ 確認送出' }}
        </button>
        
        <button 
          class="cancel-btn"
          :disabled="submitting"
          @click="handleCancel"
        >
          ❌ 取消
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, inject, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import type { CorrectPriceRequest, CorrectPriceResponse } from '@/types/api'
import { useApi } from '@/composables/useApi'
import type { LiffService } from '@/composables/useLiff'
import NumPad from '@/components/NumPad.vue'
import ErrorPage from '@/components/ErrorPage.vue'
import { showToast } from '@/components/Toast.vue'

const route = useRoute()
const liff = inject<LiffService>('liff')!
const api = useApi(() => liff.getToken())

const inputValue = ref('')
const submitting = ref(false)
const validationError = ref('')
const itemInfo = ref({
  name: '載入中...',
  currentPrice: 0
})

// QueryString 參數
const itemId = computed(() => {
  const value = route.query.itemId || route.query.item_id // 支援新舊格式
  return typeof value === 'string' ? value : ''
})
const field = computed(() => {
  const value = route.query.field
  // Graceful fallback: 缺少 field 參數時預設為 buy_price
  return typeof value === 'string' ? value : 'buy_price'
})

// 新增：從 URL 讀取品項資料
const itemName = computed(() => {
  const value = route.query.itemName
  return typeof value === 'string' ? value : '未知品項'
})
const buyPrice = computed(() => {
  const value = route.query.buyPrice
  const price = parseInt(typeof value === 'string' ? value : '0', 10)
  return isNaN(price) ? 0 : price
})
const sellPrice = computed(() => {
  const value = route.query.sellPrice
  const price = parseInt(typeof value === 'string' ? value : '0', 10)
  return isNaN(price) ? 0 : price
})

// 顯示相關計算
const displayValue = computed(() => inputValue.value || '0')
const canSubmit = computed(() => {
  const value = parseInt(inputValue.value)
  return value > 0 && value <= 99999
})

const fieldDisplayName = computed(() => {
  return field.value === 'buy_price' ? '進貨價' : '銷售價'
})

// 驗證 QueryString
function validateQueryParams(): void {
  const itemIdPattern = /^[0-9a-f]{32}$/
  const validFields = ['buy_price', 'sell_price']
  
  if (!itemId.value) {
    validationError.value = '缺少品項 ID 參數'
    return
  }
  
  if (!itemIdPattern.test(itemId.value)) {
    validationError.value = '無效的品項 ID'
    return
  }
  
  // 移除 field 參數的強制檢查，改為在 computed 中處理 fallback
  if (!validFields.includes(field.value)) {
    validationError.value = '無效的欄位參數'
    return
  }
}

// 載入品項資訊
async function loadItemInfo(): Promise<void> {
  try {
    // 檢查是否缺少必要參數
    if (buyPrice.value === 0) {
      console.warn('URL 缺少 buyPrice 參數或值為 0')
    }
    if (sellPrice.value === 0) {
      console.warn('URL 缺少 sellPrice 參數或值為 0')
    }
    
    // 從 URL query string 讀取品項資料
    itemInfo.value = {
      name: itemName.value,
      currentPrice: field.value === 'buy_price' ? buyPrice.value : sellPrice.value
    }
  } catch (error) {
    console.error('載入品項資訊失敗:', error)
    itemInfo.value = {
      name: '未知品項',
      currentPrice: 0
    }
  }
}

// 數字鍵盤事件處理
function handleInput(digit: string): void {
  const current = inputValue.value
  
  if (digit === '00') {
    if (current === '') {
      inputValue.value = '0'
    } else if (current.length <= 3) {
      inputValue.value = current + '00'
    }
  } else {
    // 防護前導零：當前值為 '0' 且輸入的不是 '00' 時，直接替換為新數字
    if (current === '0' && digit !== '00') {
      inputValue.value = digit
      return
    }
    
    if (current.length < 5) {
      inputValue.value = current + digit
    }
  }
}

function handleBackspace(): void {
  if (inputValue.value.length > 0) {
    inputValue.value = inputValue.value.slice(0, -1)
  }
}

// 送出修正
async function handleSubmit(): Promise<void> {
  if (!canSubmit.value || submitting.value) return
  
  submitting.value = true
  
  try {
    const value = parseInt(inputValue.value)
    const requestBody: CorrectPriceRequest = {}
    
    if (field.value === 'buy_price') {
      requestBody.buy_price = value
    } else {
      requestBody.sell_price = value
    }
    
    const response = await api.patch<CorrectPriceResponse>(
      `/api/draft/item/${itemId.value}`,
      requestBody
    )
    
    // 顯示成功訊息
    const message = response.validation.status === 'passed' 
      ? '✅ 修正成功！' 
      : `⚠️ ${response.validation.message || '修正完成'}`
    
    showToast(message, response.validation.status === 'passed' ? 'success' : 'warning')
    
    // 1 秒後關閉 LIFF
    setTimeout(() => {
      liff.closeWindow()
    }, 1000)
    
  } catch (error: any) {
    console.error('修正失敗:', error)
    
    if (error?.error?.code === 'UNAUTHORIZED' || error?.error?.message?.includes('401')) {
      showToast('登入已過期，請重新開啟', 'error')
      setTimeout(() => liff.closeWindow(), 1500)
    } else if (error?.error?.code === 'NOT_FOUND' || error?.error?.message?.includes('404')) {
      showToast('找不到指定的品項', 'error')
    } else {
      const message = error?.error?.message || '修正失敗，請稍後再試'
      showToast(`❌ ${message}`, 'error')
    }
  } finally {
    submitting.value = false
  }
}

// 取消操作
function handleCancel(): void {
  liff.closeWindow()
}

// 初始化
onMounted(() => {
  if (typeof route.query.field !== 'string') {
    console.warn('URL 缺少 field 參數，預設使用 buy_price')
  }
  validateQueryParams()
  if (!validationError.value) {
    loadItemInfo()
  }
})
</script>

<style scoped>
.numpad-page {
  min-height: 100vh;
  background-color: var(--bg-color);
  padding: var(--spacing-md);
}

.numpad-container {
  max-width: 400px;
  margin: 0 auto;
}

.header {
  margin-bottom: var(--spacing-lg);
}

.title {
  font-size: 1.5rem;
  font-weight: bold;
  margin-bottom: var(--spacing-sm);
  color: var(--text-primary);
  text-align: center;
}

.item-info {
  background-color: var(--card-bg);
  padding: var(--spacing-md);
  border-radius: var(--radius-md);
  border: 1px solid var(--border-color);
}

.item-name,
.field-name,
.current-price {
  margin: var(--spacing-xs) 0;
  font-size: 1rem;
  color: var(--text-primary);
}

.price-display {
  background-color: var(--card-bg);
  border: 2px solid var(--primary-color);
  border-radius: var(--radius-md);
  padding: var(--spacing-lg);
  text-align: center;
  margin-bottom: var(--spacing-lg);
  min-height: 80px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.currency {
  font-size: 2rem;
  color: var(--text-secondary);
  margin-right: var(--spacing-xs);
}

.amount {
  font-size: 2.5rem;
  font-weight: bold;
  color: var(--primary-color);
  font-family: 'SF Pro Display', -apple-system, sans-serif;
}

.actions {
  margin-top: var(--spacing-lg);
}

.confirm-btn,
.cancel-btn {
  width: 100%;
  padding: var(--spacing-md);
  border: none;
  border-radius: var(--radius-md);
  font-size: 1.1rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
  margin-bottom: var(--spacing-md);
}

.confirm-btn {
  background-color: var(--success-color);
  color: white;
}

.confirm-btn:hover:not(:disabled) {
  background-color: var(--success-hover);
}

.confirm-btn:disabled {
  background-color: var(--disabled-color);
  cursor: not-allowed;
}

.cancel-btn {
  background-color: var(--error-color);
  color: white;
}

.cancel-btn:hover:not(:disabled) {
  background-color: var(--error-hover);
}

.cancel-btn:disabled {
  background-color: var(--disabled-color);
  cursor: not-allowed;
}
</style>