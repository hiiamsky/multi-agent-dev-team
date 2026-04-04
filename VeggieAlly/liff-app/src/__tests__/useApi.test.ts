import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { useApi } from '@/composables/useApi'

const mockFetch = vi.fn()

describe('useApi', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', mockFetch)
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  const getToken = () => 'test-access-token'

  it('attaches_bearer_token — includes Authorization header', async () => {
    mockFetch.mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({ data: 'ok' })
    })

    const api = useApi(getToken)
    await api.get('/api/test')

    expect(mockFetch).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: 'Bearer test-access-token'
        })
      })
    )
  })

  it('parses_json_response — returns parsed body on 200', async () => {
    const responseData = { id: '123', name: '高麗菜' }
    mockFetch.mockResolvedValue({
      ok: true,
      json: () => Promise.resolve(responseData)
    })

    const api = useApi(getToken)
    const result = await api.get('/api/test')

    expect(result).toEqual(responseData)
  })

  it('throws_api_error_on_4xx — throws structured error on 400', async () => {
    const errorBody = { error: { code: 'VALIDATION', message: '驗證失敗' } }
    mockFetch.mockResolvedValue({
      ok: false,
      status: 400,
      statusText: 'Bad Request',
      json: () => Promise.resolve(errorBody)
    })

    const api = useApi(getToken)
    await expect(api.get('/api/test')).rejects.toEqual(errorBody)
    expect(api.error.value).toEqual(errorBody)
  })

  it('sets_loading_during_request — loading is true during request, false after', async () => {
    let resolvePromise: (value: any) => void
    const pendingResponse = new Promise(resolve => { resolvePromise = resolve })

    mockFetch.mockReturnValue(pendingResponse)

    const api = useApi(getToken)
    const promise = api.get('/api/test')

    // Loading should be true while request is in-flight
    expect(api.loading.value).toBe(true)

    resolvePromise!({
      ok: true,
      json: () => Promise.resolve({ data: 'ok' })
    })

    await promise
    expect(api.loading.value).toBe(false)
  })

  it('handles_network_error — wraps fetch TypeError as network error', async () => {
    mockFetch.mockRejectedValue(new TypeError('Failed to fetch'))

    const api = useApi(getToken)
    await expect(api.get('/api/test')).rejects.toEqual(
      expect.objectContaining({
        error: expect.objectContaining({
          code: 'NETWORK_ERROR'
        })
      })
    )
  })
})
