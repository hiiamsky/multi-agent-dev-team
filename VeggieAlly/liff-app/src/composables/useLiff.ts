import { ref, type Ref } from 'vue'
import liff from '@line/liff'

export interface LiffService {
  isReady: Ref<boolean>
  accessToken: Ref<string | null>
  userId: Ref<string | null>
  error: Ref<string | null>
  init(liffId: string): Promise<void>
  getToken(): string
  closeWindow(): void
  getUserId(): string | null
}

let liffInstance: LiffService | null = null

export function useLiff(): LiffService {
  if (liffInstance) {
    return liffInstance
  }

  const isReady: Ref<boolean> = ref(false)
  const accessToken: Ref<string | null> = ref(null)
  const userId: Ref<string | null> = ref(null)
  const error: Ref<string | null> = ref(null)

  async function init(liffId: string): Promise<void> {
    try {
      await liff.init({ liffId })
      
      if (!liff.isLoggedIn()) {
        throw new Error('用戶未登入 LINE')
      }

      // 取得 access token 和 user ID
      accessToken.value = liff.getAccessToken()
      const profile = await liff.getProfile()
      userId.value = profile.userId

      if (!accessToken.value) {
        throw new Error('無法取得存取權杖')
      }

      isReady.value = true
      error.value = null
    } catch (err) {
      console.error('LIFF 初始化錯誤:', err)
      error.value = err instanceof Error ? err.message : '初始化失敗'
      isReady.value = false
      throw err
    }
  }

  function getToken(): string {
    if (!isReady.value || !accessToken.value) {
      throw new Error('LIFF 尚未就緒或無效的存取權杖')
    }
    return accessToken.value
  }

  function closeWindow(): void {
    if (liff.isInClient()) {
      liff.closeWindow()
    } else {
      // 在外部瀏覽器中，顯示提示
      alert('請在 LINE 應用中開啟此頁面')
    }
  }

  function getUserId(): string | null {
    return userId.value
  }

  liffInstance = {
    isReady,
    accessToken,
    userId,
    error,
    init,
    getToken,
    closeWindow,
    getUserId
  }

  return liffInstance
}