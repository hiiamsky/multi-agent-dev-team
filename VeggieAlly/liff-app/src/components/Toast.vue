<template>
  <Teleport to="body">
    <div v-if="isVisible" :class="['toast', type]" @click="hide">
      <div class="toast-content">
        <span class="toast-message">{{ message }}</span>
      </div>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { ref } from 'vue'

type ToastType = 'success' | 'error' | 'warning' | 'info'

const isVisible = ref(false)
const message = ref('')
const type = ref<ToastType>('info')
let hideTimer: NodeJS.Timeout | null = null

function show(msg: string, toastType: ToastType = 'info', duration = 3000): void {
  message.value = msg
  type.value = toastType
  isVisible.value = true

  // 清除之前的計時器
  if (hideTimer) {
    clearTimeout(hideTimer)
  }

  // 設定自動隱藏
  hideTimer = setTimeout(() => {
    hide()
  }, duration)
}

function hide(): void {
  isVisible.value = false
  if (hideTimer) {
    clearTimeout(hideTimer)
    hideTimer = null
  }
}

// 暴露方法供外部呼叫
defineExpose({
  show,
  hide
})
</script>

<script lang="ts">
type GlobalToastType = 'success' | 'error' | 'warning' | 'info'

// 全域 toast 實例
let toastInstance: { show: (msg: string, type?: GlobalToastType, duration?: number) => void } | null = null

// 全域 showToast 函數
export function showToast(message: string, type: GlobalToastType = 'info', duration = 3000): void {
  if (toastInstance) {
    toastInstance.show(message, type, duration)
  } else {
    // Fallback 到 console
    console.log(`[Toast ${type}]`, message)
  }
}

// 註冊 toast 實例
export function registerToast(instance: { show: (msg: string, type?: GlobalToastType, duration?: number) => void }): void {
  toastInstance = instance
}
</script>

<style scoped>
.toast {
  position: fixed;
  top: 20px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 9999;
  max-width: 90%;
  min-width: 200px;
  padding: var(--spacing-md) var(--spacing-lg);
  border-radius: var(--radius-md);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
  cursor: pointer;
  animation: toastSlideIn 0.3s ease-out;
  font-size: 1rem;
  font-weight: 500;
}

.toast-content {
  display: flex;
  align-items: center;
  justify-content: center;
}

.toast-message {
  text-align: center;
}

/* Toast 類型樣式 */
.toast.success {
  background-color: var(--success-color);
  color: white;
}

.toast.error {
  background-color: var(--error-color);
  color: white;
}

.toast.warning {
  background-color: var(--warning-color);
  color: #333;
}

.toast.info {
  background-color: var(--info-color);
  color: white;
}

@keyframes toastSlideIn {
  from {
    opacity: 0;
    transform: translateX(-50%) translateY(-20px);
  }
  to {
    opacity: 1;
    transform: translateX(-50%) translateY(0);
  }
}

/* 確保在小螢幕上也能正常顯示 */
@media (max-width: 480px) {
  .toast {
    max-width: calc(100% - 40px);
    margin: 0 20px;
  }
}
</style>