<template>
  <div id="app">
    <router-view />
    <Toast ref="toastRef" />
  </div>
</template>

<script setup lang="ts">
import { provide, ref, onMounted } from 'vue'
import { useLiff, type LiffService } from './composables/useLiff'
import Toast, { registerToast } from './components/Toast.vue'

// 提供 LIFF 實例給子元件使用
const liff = useLiff()
provide('liff', liff)

// Toast 實例
const toastRef = ref()

onMounted(() => {
  if (toastRef.value) {
    registerToast(toastRef.value)
  }
})
</script>

<style scoped>
#app {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
}
</style>