import { createApp } from 'vue'
import App from './App.vue'
import router from './router'
import { useLiff } from './composables/useLiff'
import './styles/global.css'

// 初始化 LIFF 和 Vue 應用
async function initApp(): Promise<void> {
  try {
    // 初始化 LIFF
    const liff = useLiff()
    const liffId = import.meta.env.VITE_LIFF_ID
    
    if (!liffId || liffId === 'your-liff-id-here') {
      console.warn('VITE_LIFF_ID 未設定，使用測試模式')
    } else {
      await liff.init(liffId)
    }

    // LIFF 初始化成功後 mount Vue app
    const app = createApp(App)
    app.use(router)
    app.mount('#app')
  } catch (error) {
    console.error('應用初始化失敗:', error)
    // 顯示錯誤頁面（使用 DOM API 避免 XSS）
    const appElement = document.getElementById('app')
    if (appElement) {
      appElement.textContent = ''
      const wrapper = document.createElement('div')
      wrapper.style.cssText = 'padding:20px;text-align:center;font-family:sans-serif'
      const h2 = document.createElement('h2')
      h2.style.color = '#e74c3c'
      h2.textContent = '🚫 無法初始化應用'
      const p1 = document.createElement('p')
      p1.textContent = error instanceof Error ? error.message : '未知錯誤'
      const p2 = document.createElement('p')
      p2.style.cssText = 'color:#666;font-size:14px'
      p2.textContent = '請檢查設定或在 LINE 中開啟此頁面'
      wrapper.append(h2, p1, p2)
      appElement.appendChild(wrapper)
    }
  }
}

// 啟動應用
initApp().catch(console.error)