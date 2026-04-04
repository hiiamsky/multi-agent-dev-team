import { describe, it, expect, vi, beforeEach } from 'vitest'

// We need to reset the singleton before each test
let useLiffModule: typeof import('@/composables/useLiff')

// Mock @line/liff
vi.mock('@line/liff', () => ({
  default: {
    init: vi.fn(),
    isLoggedIn: vi.fn(),
    getAccessToken: vi.fn(),
    getProfile: vi.fn(),
    isInClient: vi.fn(),
    closeWindow: vi.fn()
  }
}))

describe('useLiff', () => {
  beforeEach(async () => {
    vi.resetModules()
    // Re-import to reset singleton
    useLiffModule = await import('@/composables/useLiff')
  })

  it('init_success_sets_ready — isReady becomes true after successful init', async () => {
    const liff = await import('@line/liff')
    const liffDefault = liff.default
    vi.mocked(liffDefault.init).mockResolvedValue(undefined)
    vi.mocked(liffDefault.isLoggedIn).mockReturnValue(true)
    vi.mocked(liffDefault.getAccessToken).mockReturnValue('mock-token')
    vi.mocked(liffDefault.getProfile).mockResolvedValue({
      userId: 'U12345',
      displayName: 'Test',
      pictureUrl: undefined as any,
      statusMessage: undefined as any
    })

    const service = useLiffModule.useLiff()
    await service.init('test-liff-id')

    expect(service.isReady.value).toBe(true)
    expect(service.accessToken.value).toBe('mock-token')
    expect(service.userId.value).toBe('U12345')
    expect(service.error.value).toBeNull()
  })

  it('init_failure_sets_error — error is set when init fails', async () => {
    const liff = await import('@line/liff')
    vi.mocked(liff.default.init).mockRejectedValue(new Error('LIFF init failed'))

    const service = useLiffModule.useLiff()

    await expect(service.init('bad-id')).rejects.toThrow('LIFF init failed')
    expect(service.isReady.value).toBe(false)
    expect(service.error.value).toBe('LIFF init failed')
  })

  it('getToken_returns_access_token — returns token when ready', async () => {
    const liff = await import('@line/liff')
    const liffDefault = liff.default
    vi.mocked(liffDefault.init).mockResolvedValue(undefined)
    vi.mocked(liffDefault.isLoggedIn).mockReturnValue(true)
    vi.mocked(liffDefault.getAccessToken).mockReturnValue('the-token')
    vi.mocked(liffDefault.getProfile).mockResolvedValue({
      userId: 'U12345',
      displayName: 'Test',
      pictureUrl: undefined as any,
      statusMessage: undefined as any
    })

    const service = useLiffModule.useLiff()
    await service.init('test-liff-id')

    expect(service.getToken()).toBe('the-token')
  })

  it('getToken_throws_if_not_ready — throws when called before init', () => {
    const service = useLiffModule.useLiff()

    expect(() => service.getToken()).toThrow('LIFF 尚未就緒')
  })
})
