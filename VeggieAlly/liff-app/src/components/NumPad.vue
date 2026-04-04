<template>
  <div class="numpad">
    <div class="numpad-grid">
      <!-- 第一排 -->
      <button class="numpad-btn" @click="$emit('input', '1')">1</button>
      <button class="numpad-btn" @click="$emit('input', '2')">2</button>
      <button class="numpad-btn" @click="$emit('input', '3')">3</button>
      
      <!-- 第二排 -->
      <button class="numpad-btn" @click="$emit('input', '4')">4</button>
      <button class="numpad-btn" @click="$emit('input', '5')">5</button>
      <button class="numpad-btn" @click="$emit('input', '6')">6</button>
      
      <!-- 第三排 -->
      <button class="numpad-btn" @click="$emit('input', '7')">7</button>
      <button class="numpad-btn" @click="$emit('input', '8')">8</button>
      <button class="numpad-btn" @click="$emit('input', '9')">9</button>
      
      <!-- 第四排 -->
      <button class="numpad-btn double-zero" @click="$emit('input', '00')">00</button>
      <button class="numpad-btn" @click="$emit('input', '0')">0</button>
      <button class="numpad-btn backspace" @click="$emit('backspace')">
        <span class="backspace-icon">⌫</span>
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
// 定義 emits
const emit = defineEmits<{
  input: [digit: string]
  backspace: []
}>()

// 定義 props（雖然這個元件不需要 props，但為了一致性保留）
interface Props {
  value?: string
}

defineProps<Props>()
</script>

<style scoped>
.numpad {
  margin: var(--spacing-md) 0;
}

.numpad-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  grid-gap: var(--spacing-sm);
  max-width: 300px;
  margin: 0 auto;
}

.numpad-btn {
  width: 80px;
  height: 80px;
  border: 2px solid var(--border-color);
  border-radius: var(--radius-md);
  background-color: var(--card-bg);
  color: var(--text-primary);
  font-size: 1.5rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
  user-select: none;
  -webkit-user-select: none;
  -webkit-touch-callout: none;
  -webkit-tap-highlight-color: transparent;
}

.numpad-btn:hover {
  background-color: var(--hover-bg);
  border-color: var(--primary-color);
}

.numpad-btn:active {
  background-color: var(--primary-color);
  color: white;
  transform: scale(0.95);
}

.double-zero {
  font-size: 1.3rem;
}

.backspace {
  background-color: var(--error-light);
  color: var(--error-color);
  border-color: var(--error-light);
}

.backspace:hover {
  background-color: var(--error-color);
  color: white;
}

.backspace-icon {
  font-size: 1.8rem;
  line-height: 1;
}

/* 確保在小螢幕上按鈕不會太小 */
@media (max-width: 360px) {
  .numpad-grid {
    grid-gap: var(--spacing-xs);
    max-width: 280px;
  }
  
  .numpad-btn {
    width: 75px;
    height: 75px;
    font-size: 1.4rem;
  }
}

/* 觸控設備優化 */
@media (hover: none) {
  .numpad-btn:hover {
    background-color: var(--card-bg);
    border-color: var(--border-color);
  }
  
  .backspace:hover {
    background-color: var(--error-light);
    color: var(--error-color);
  }
}
</style>