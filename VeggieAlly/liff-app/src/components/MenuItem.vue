<template>
  <div class="menu-item" :class="{ 'has-order': hasOrder }">
    <div class="item-header">
      <h3 class="item-name" :class="{ 'ordered': hasOrder }">
        {{ item.name }}
        <span v-if="item.is_new" class="new-badge">新品</span>
      </h3>
      <div class="item-price">
        ${{ item.sell_price }}/{{ item.unit }}
      </div>
    </div>
    
    <div class="item-stock" :class="{ 'out-of-stock': isOutOfStock }">
      庫存：{{ item.remaining_qty }}/{{ item.original_qty ?? item.remaining_qty }}
    </div>
    
    <div class="order-controls">
      <span class="order-label">下單：</span>
      <button 
        class="qty-btn minus"
        :disabled="item.orderQty <= 0"
        @click="decreaseQty"
      >
        -
      </button>
      <span class="qty-display">{{ item.orderQty }}</span>
      <button 
        class="qty-btn plus"
        :disabled="item.orderQty >= item.remaining_qty"
        @click="increaseQty"
      >
        +
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { OrderItem } from '@/types/api'

interface Props {
  item: OrderItem
}

const props = defineProps<Props>()

const emit = defineEmits<{
  'qty-change': [itemId: string, newQty: number]
}>()

const isOutOfStock = computed(() => props.item.remaining_qty <= 0)
const hasOrder = computed(() => props.item.orderQty > 0)

function increaseQty(): void {
  if (props.item.orderQty < props.item.remaining_qty) {
    emit('qty-change', props.item.id, props.item.orderQty + 1)
  }
}

function decreaseQty(): void {
  if (props.item.orderQty > 0) {
    emit('qty-change', props.item.id, props.item.orderQty - 1)
  }
}
</script>

<style scoped>
.menu-item {
  background-color: var(--card-bg);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-md);
  padding: var(--spacing-md);
  margin-bottom: var(--spacing-sm);
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.item-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: var(--spacing-sm);
}

.item-name {
  font-size: 1.1rem;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0;
  display: flex;
  align-items: center;
  gap: var(--spacing-xs);
}

.new-badge {
  background-color: var(--success-color);
  color: white;
  font-size: 0.7rem;
  padding: 2px 6px;
  border-radius: 10px;
  font-weight: 500;
}

.item-price {
  font-size: 1.1rem;
  font-weight: 700;
  color: var(--primary-color);
}

.item-stock {
  font-size: 0.9rem;
  color: var(--text-secondary);
  margin-bottom: var(--spacing-md);
}

.order-controls {
  display: flex;
  align-items: center;
  gap: var(--spacing-sm);
}

.order-label {
  font-size: 0.9rem;
  color: var(--text-secondary);
  min-width: 40px;
}

.qty-btn {
  width: 36px;
  height: 36px;
  border: 1px solid var(--border-color);
  border-radius: var(--radius-sm);
  background-color: var(--card-bg);
  color: var(--text-primary);
  font-size: 1.2rem;
  font-weight: bold;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
  user-select: none;
  -webkit-user-select: none;
}

.qty-btn:hover:not(:disabled) {
  background-color: var(--primary-color);
  color: white;
  border-color: var(--primary-color);
}

.qty-btn:disabled {
  background-color: var(--disabled-bg);
  color: var(--disabled-color);
  cursor: not-allowed;
}

.qty-btn.minus:disabled {
  opacity: 0.5;
}

.qty-btn.plus:disabled {
  opacity: 0.5;
}

.qty-display {
  min-width: 30px;
  text-align: center;
  font-size: 1.1rem;
  font-weight: 600;
  color: var(--text-primary);
}

/* 庫存不足時的樣式 */
.item-stock.out-of-stock {
  color: var(--warning-color);
}

/* 已選擇數量時的高亮效果 */
.item-name.ordered {
  color: var(--primary-color);
}

@media (hover: none) {
  .qty-btn:hover:not(:disabled) {
    background-color: var(--card-bg);
    color: var(--text-primary);
    border-color: var(--border-color);
  }
}
</style>