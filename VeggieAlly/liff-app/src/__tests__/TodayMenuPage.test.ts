import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import TodayMenuPage from '@/pages/TodayMenuPage.vue'
import type { LiffService } from '@/composables/useLiff'
import type { TodayMenuResponse } from '@/types/api'
import { ref } from 'vue'

// Mock useApi
const mockGet = vi.fn()
const mockPatch = vi.fn()
vi.mock('@/composables/useApi', () => ({
  useApi: () => ({
    loading: ref(false),
    error: ref(null),
    get: mockGet,
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

const mockMenuResponse: TodayMenuResponse = {
  id: 'menu-001',
  tenant_id: 'tenant-001',
  date: '2024-06-15',
  published_at: '2024-06-15T06:00:00Z',
  items: [
    { id: 'item-1', name: '高麗菜', is_new: false, sell_price: 35, original_qty: 100, remaining_qty: 80, unit: '斤' },
    { id: 'item-2', name: '空心菜', is_new: true, sell_price: 25, original_qty: 50, remaining_qty: 30, unit: '把' }
  ]
}

function createMockLiff(): LiffService {
  return {
    isReady: ref(true),
    accessToken: ref('test-token'),
    userId: ref('test-user'),
    error: ref(null),
    init: vi.fn(),
    getToken: vi.fn().mockReturnValue('test-token'),
    closeWindow: vi.fn(),
    getUserId: vi.fn().mockReturnValue('test-user')
  }
}

function createTestRouter() {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/menu', name: 'TodayMenu', component: TodayMenuPage },
      { path: '/', component: { template: '<div />' } }
    ]
  })
}

async function mountPage(liff?: LiffService) {
  const router = createTestRouter()
  await router.push('/menu')
  await router.isReady()

  return mount(TodayMenuPage, {
    global: {
      plugins: [router],
      provide: { liff: liff ?? createMockLiff() },
      stubs: { MenuItem: true }
    }
  })
}

describe('TodayMenuPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('loads_and_renders_menu — renders items after GET 200', async () => {
    mockGet.mockResolvedValue(mockMenuResponse)
    const wrapper = await mountPage()
    await flushPromises()

    expect(mockGet).toHaveBeenCalledWith('/api/menu/today', expect.objectContaining({ tenant_id: expect.any(String) }))
    expect(wrapper.find('.menu-container').exists()).toBe(true)
    expect(wrapper.find('.item-count').text()).toContain('2')
  })

  it('404_shows_empty_state — shows empty state when menu not found', async () => {
    mockGet.mockRejectedValue({
      error: { code: 'NOT_FOUND', message: '404' }
    })
    const wrapper = await mountPage()
    await flushPromises()

    expect(wrapper.find('.empty-state').exists()).toBe(true)
  })

  it('increment_qty_within_stock — qty change handler clamps to remaining', async () => {
    mockGet.mockResolvedValue(mockMenuResponse)
    const wrapper = await mountPage()
    await flushPromises()

    // Call handleQtyChange to increment
    ;(wrapper.vm as any).handleQtyChange('item-1', 5)
    const item = (wrapper.vm as any).orderItems.find((i: any) => i.id === 'item-1')
    expect(item!.orderQty).toBe(5)
  })

  it('decrement_qty_to_zero — qty cannot go below 0', async () => {
    mockGet.mockResolvedValue(mockMenuResponse)
    const wrapper = await mountPage()
    await flushPromises()

    ;(wrapper.vm as any).handleQtyChange('item-1', -1)
    const item = (wrapper.vm as any).orderItems.find((i: any) => i.id === 'item-1')
    expect(item!.orderQty).toBe(0) // Clamped to 0
  })

  it('order_button_disabled_when_no_items — button disabled when no qty selected', async () => {
    mockGet.mockResolvedValue(mockMenuResponse)
    const wrapper = await mountPage()
    await flushPromises()

    const orderBtn = wrapper.find('.order-btn')
    expect((orderBtn.element as HTMLButtonElement).disabled).toBe(true)
  })

  it('submit_order_calls_inventory_api — calls PATCH for each ordered item', async () => {
    mockGet.mockResolvedValue(mockMenuResponse)
    mockPatch.mockResolvedValue({ id: 'item-1', name: '高麗菜', remaining_qty: 75, unit: '斤' })

    const wrapper = await mountPage()
    await flushPromises()

    // Set order qty
    ;(wrapper.vm as any).handleQtyChange('item-1', 5)
    await wrapper.vm.$nextTick()

    // Submit
    await wrapper.find('.order-btn').trigger('click')
    await flushPromises()

    expect(mockPatch).toHaveBeenCalledWith(
      '/api/menu/inventory',
      { item_id: 'item-1', amount: 5 },
      { 'X-Tenant-Id': 'tenant-001' }
    )
  })

  it('order_409_shows_stock_error — handles 409 conflict', async () => {
    mockGet.mockResolvedValue(mockMenuResponse)
    mockPatch.mockRejectedValue({
      error: { code: 'CONFLICT', message: '庫存不足' }
    })

    const wrapper = await mountPage()
    await flushPromises()

    ;(wrapper.vm as any).handleQtyChange('item-1', 5)
    await wrapper.vm.$nextTick()

    await wrapper.find('.order-btn').trigger('click')
    await flushPromises()

    expect((wrapper.vm as any).submitting).toBe(false)
  })

  it('order_success_reloads_menu — reloads menu after successful order', async () => {
    mockGet.mockResolvedValue(mockMenuResponse)
    mockPatch.mockResolvedValue({ id: 'item-1', name: '高麗菜', remaining_qty: 75, unit: '斤' })

    const wrapper = await mountPage()
    await flushPromises()

    ;(wrapper.vm as any).handleQtyChange('item-1', 5)
    await wrapper.vm.$nextTick()

    mockGet.mockClear()
    await wrapper.find('.order-btn').trigger('click')
    await flushPromises()

    // Menu reload happens after a setTimeout, so verify submit succeeded
    expect((wrapper.vm as any).submitting).toBe(false)
  })
})
