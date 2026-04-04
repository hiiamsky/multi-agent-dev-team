import { ref, type Ref } from 'vue'
import type { ApiError } from '@/types/api'

export function useApi(getToken: () => string) {
  const loading: Ref<boolean> = ref(false)
  const error: Ref<ApiError | null> = ref(null)

  const baseUrl = import.meta.env.VITE_API_BASE_URL || window.location.origin

  async function makeRequest<T>(
    method: string,
    path: string,
    options: {
      body?: object
      params?: Record<string, string>
      headers?: Record<string, string>
    } = {}
  ): Promise<T> {
    loading.value = true
    error.value = null

    try {
      const url = new URL(path, baseUrl)
      
      // 添加 query parameters
      if (options.params) {
        Object.entries(options.params).forEach(([key, value]) => {
          url.searchParams.append(key, value)
        })
      }

      const headers: Record<string, string> = {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${getToken()}`,
        ...options.headers
      }

      const fetchOptions: RequestInit = {
        method,
        headers
      }

      if (options.body && (method === 'POST' || method === 'PATCH' || method === 'PUT')) {
        fetchOptions.body = JSON.stringify(options.body)
      }

      const response = await fetch(url.toString(), fetchOptions)

      if (!response.ok) {
        // 嘗試解析錯誤回應
        let apiError: ApiError
        try {
          const errorBody = await response.json()
          apiError = errorBody as ApiError
        } catch {
          // 解析失敗，建立通用錯誤
          const codeMap: Record<number, string> = {
            400: 'BAD_REQUEST',
            401: 'UNAUTHORIZED',
            403: 'FORBIDDEN',
            404: 'NOT_FOUND',
            409: 'CONFLICT',
            500: 'INTERNAL_ERROR'
          }
          apiError = {
            error: {
              code: codeMap[response.status] || `HTTP_${response.status}`,
              message: response.statusText || '請求失敗'
            }
          }
        }
        
        error.value = apiError
        throw apiError
      }

      // 成功回應
      const result = await response.json()
      return result as T
    } catch (err) {
      if (err instanceof TypeError && err.message.includes('fetch')) {
        // 網路錯誤
        const networkError: ApiError = {
          error: {
            code: 'NETWORK_ERROR',
            message: '網路連線異常，請稍後再試'
          }
        }
        error.value = networkError
        throw networkError
      }
      
      // 重新拋出 API 錯誤
      throw err
    } finally {
      loading.value = false
    }
  }

  async function get<T>(path: string, params?: Record<string, string>): Promise<T> {
    return makeRequest<T>('GET', path, params ? { params } : {})
  }

  async function patch<T>(path: string, body: object, headers?: Record<string, string>): Promise<T> {
    return makeRequest<T>('PATCH', path, { body, ...(headers && { headers }) })
  }

  async function post<T>(path: string, body: object, headers?: Record<string, string>): Promise<T> {
    return makeRequest<T>('POST', path, { body, ...(headers && { headers }) })
  }

  return {
    loading,
    error,
    get,
    patch,
    post
  }
}