import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import NumPadPage from '@/pages/NumPadPage.vue'
import type { LiffService } from '@/composables/useLiff'
import { ref } from 'vue'

// Mock useApi
const mockPatch = vi.fn()
vi.mock('@/composables/useApi', () => ({
  useApi: () => ({
    loading: ref(false),
    error: ref(null),
    get: vi.fn(),
    patch: mockPatch,
    post: vi.fn()
  })
}))

// Mock Toast
vi.mock('@/components/Toast.vue', async () => {
  const actual = await vi.importActual<any>('@/components/Toast.vue')
  return {
    ...actual,
    showToast: vi.fn()
  }
})

function createMockLiff(overrides: Partial<LiffService> = {}): LiffService {
  return {
    isReady: ref(true),
    accessToken: ref('test-token'),
    userId: ref('test-user'),
    error: ref(null),
    init: vi.fn(),
    getToken: vi.fn().mockReturnValue('test-token'),
    closeWindow: vi.fn(),
    getUserId: vi.fn().mockReturnValue('test-user'),
    ...overrides
  }
}

function createTestRouter(query: Record<string, string> = {}) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/numpad', name: 'NumPad', component: NumPadPage },
      { path: '/', component: { template: '<div />' } }
    ]
  })
  return router
}

async function mountPage(query: Record<string, string>, liff?: LiffService) {
  const router = createTestRouter()
  await router.push({ path: '/numpad', query })
  await router.isReady()

  const mockLiff = liff ?? createMockLiff()

  return mount(NumPadPage, {
    global: {
      plugins: [router],
      provide: { liff: mockLiff },
      stubs: { NumPad: true, ErrorPage: true }
    }
  })
}

const validQuery = {
  item_id: 'a'.repeat(32),
  field: 'buy_price'
}

describe('NumPadPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders_item_info_from_query — shows item info when valid query', async () => {
    const wrapper = await mountPage(validQuery)
    await flushPromises()

    expect(wrapper.find('.field-name').text()).toContain('進貨價')
    expect(wrapper.find('.title').text()).toContain('修正價格')
  })

  it('invalid_item_id_shows_error — shows ErrorPage for non 32-hex item_id', async () => {
    const wrapper = await mountPage({ item_id: 'invalid', field: 'buy_price' })
    await flushPromises()

    expect(wrapper.findComponent({ name: 'ErrorPage' }).exists()).toBe(true)
  })

  it('invalid_field_shows_error — shows ErrorPage for invalid field value', async () => {
    const wrapper = await mountPage({ item_id: 'a'.repeat(32), field: 'quantity' })
    await flushPromises()

    expect(wrapper.findComponent({ name: 'ErrorPage' }).exists()).toBe(true)
  })

  it('submit_calls_patch_api — calls PATCH with correct body', async () => {
    mockPatch.mockResolvedValue({
      id: 'a'.repeat(32),
      name: '高麗菜',
      validation: { status: 'passed', message: null }
    })

    const wrapper = await mountPage(validQuery)
    await flushPromises()

    // Simulate input
    ;(wrapper.vm as any).inputValue = '25'
    await wrapper.vm.$nextTick()

    // Submit
    await wrapper.find('.confirm-btn').trigger('click')
    await flushPromises()

    expect(mockPatch).toHaveBeenCalledWith(
      `/api/draft/item/${'a'.repeat(32)}`,
      { buy_price: 25 }
    )
  })

  it('submit_success_shows_toast_and_closes — closes LIFF after success', async () => {
    vi.useFakeTimers()
    const mockLiff = createMockLiff()
    mockPatch.mockResolvedValue({
      id: 'a'.repeat(32),
      name: '高麗菜',
      validation: { status: 'passed', message: null }
    })

    const wrapper = await mountPage(validQuery, mockLiff)
    await flushPromises()

    ;(wrapper.vm as any).inputValue = '25'
    await wrapper.vm.$nextTick()
    await wrapper.find('.confirm-btn').trigger('click')
    await flushPromises()

    // closeWindow is called after 1 second timeout
    vi.advanceTimersByTime(1000)
    expect(mockLiff.closeWindow).toHaveBeenCalled()
    vi.useRealTimers()
  })

  it('submit_401_shows_expired — handles 401 error', async () => {
    mockPatch.mockRejectedValue({
      error: { code: 'UNAUTHORIZED', message: '401' }
    })

    const wrapper = await mountPage(validQuery)
    await flushPromises()

    ;(wrapper.vm as any).inputValue = '25'
    await wrapper.vm.$nextTick()
    await wrapper.find('.confirm-btn').trigger('click')
    await flushPromises()

    expect((wrapper.vm as any).submitting).toBe(false)
  })

  it('submit_404_shows_not_found — handles 404 error', async () => {
    mockPatch.mockRejectedValue({
      error: { code: 'NOT_FOUND', message: '404' }
    })

    const wrapper = await mountPage(validQuery)
    await flushPromises()

    ;(wrapper.vm as any).inputValue = '25'
    await wrapper.vm.$nextTick()
    await wrapper.find('.confirm-btn').trigger('click')
    await flushPromises()

    expect((wrapper.vm as any).submitting).toBe(false)
  })

  it('cancel_closes_liff — calls closeWindow on cancel', async () => {
    const mockLiff = createMockLiff()

    const wrapper = await mountPage(validQuery, mockLiff)
    await flushPromises()

    await wrapper.find('.cancel-btn').trigger('click')

    expect(mockLiff.closeWindow).toHaveBeenCalled()
  })
})
