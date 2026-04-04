<template>
  <div class="error-page">
    <div class="error-content">
      <div class="error-icon">🚫</div>
      <h2 class="error-title">{{ title || '發生錯誤' }}</h2>
      <p v-if="message" class="error-message">{{ message }}</p>
      <div class="error-actions">
        <button v-if="showCloseButton" @click="handleClose" class="close-btn">
          關閉頁面
        </button>
        <button v-if="showRetryButton" @click="$emit('retry')" class="retry-btn">
          重試
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { inject } from 'vue'
import type { LiffService } from '@/composables/useLiff'

interface Props {
  title?: string
  message?: string
  showCloseButton?: boolean
  showRetryButton?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  showCloseButton: true,
  showRetryButton: false
})

const emit = defineEmits<{
  retry: []
}>()

const liff = inject<LiffService>('liff')

function handleClose(): void {
  if (liff) {
    liff.closeWindow()
  } else {
    // Fallback: 嘗試關閉視窗
    window.close()
  }
}
</script>

<style scoped>
.error-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: var(--bg-color);
  padding: var(--spacing-lg);
}

.error-content {
  text-align: center;
  max-width: 400px;
  width: 100%;
}

.error-icon {
  font-size: 4rem;
  margin-bottom: var(--spacing-lg);
}

.error-title {
  font-size: 1.5rem;
  font-weight: 600;
  color: var(--error-color);
  margin-bottom: var(--spacing-md);
}

.error-message {
  font-size: 1rem;
  color: var(--text-secondary);
  margin-bottom: var(--spacing-lg);
  line-height: 1.5;
}

.error-actions {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-sm);
}

.close-btn,
.retry-btn {
  padding: var(--spacing-md) var(--spacing-lg);
  border: none;
  border-radius: var(--radius-md);
  font-size: 1rem;
  font-weight: 600;
  cursor: pointer;
  transition: background-color 0.2s;
}

.close-btn {
  background-color: var(--error-color);
  color: white;
}

.close-btn:hover {
  background-color: var(--error-hover);
}

.retry-btn {
  background-color: var(--primary-color);
  color: white;
}

.retry-btn:hover {
  background-color: var(--primary-hover);
}

/* 水平排列按鈕（當兩個都存在時） */
.error-actions:has(.close-btn):has(.retry-btn) {
  flex-direction: row;
}

.error-actions:has(.close-btn):has(.retry-btn) .close-btn,
.error-actions:has(.close-btn):has(.retry-btn) .retry-btn {
  flex: 1;
}
</style>