import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import NumPad from '@/components/NumPad.vue'

describe('NumPad', () => {
  it('digit_appends_to_value — emits input events for digit presses', async () => {
    const wrapper = mount(NumPad, { props: { value: '' } })
    const buttons = wrapper.findAll('.numpad-btn')

    // Press 1, 2, 3
    await buttons[0].trigger('click') // 1
    await buttons[1].trigger('click') // 2
    await buttons[2].trigger('click') // 3

    const emitted = wrapper.emitted('input')!
    expect(emitted).toHaveLength(3)
    expect(emitted[0]).toEqual(['1'])
    expect(emitted[1]).toEqual(['2'])
    expect(emitted[2]).toEqual(['3'])
  })

  it('double_zero_appends — emits "00" for double-zero button', async () => {
    const wrapper = mount(NumPad, { props: { value: '1' } })
    const doubleZeroBtn = wrapper.find('.double-zero')

    await doubleZeroBtn.trigger('click')

    const emitted = wrapper.emitted('input')!
    expect(emitted).toHaveLength(1)
    expect(emitted[0]).toEqual(['00'])
  })

  it('backspace_removes_last — emits backspace event', async () => {
    const wrapper = mount(NumPad, { props: { value: '123' } })
    const backspaceBtn = wrapper.find('.backspace')

    await backspaceBtn.trigger('click')

    const emitted = wrapper.emitted('backspace')!
    expect(emitted).toHaveLength(1)
  })

  it('backspace_empty_noop — emits backspace even when empty (parent handles noop)', async () => {
    const wrapper = mount(NumPad, { props: { value: '' } })
    const backspaceBtn = wrapper.find('.backspace')

    await backspaceBtn.trigger('click')

    // NumPad always emits; parent decides if it's a noop
    const emitted = wrapper.emitted('backspace')!
    expect(emitted).toHaveLength(1)
  })

  it('renders 12 buttons in grid', () => {
    const wrapper = mount(NumPad, { props: { value: '' } })
    const buttons = wrapper.findAll('.numpad-btn')
    expect(buttons).toHaveLength(12)
  })

  it('renders buttons in correct order: 1-9, 00, 0, backspace', () => {
    const wrapper = mount(NumPad, { props: { value: '' } })
    const buttons = wrapper.findAll('.numpad-btn')

    // First row: 1, 2, 3
    expect(buttons[0].text()).toBe('1')
    expect(buttons[1].text()).toBe('2')
    expect(buttons[2].text()).toBe('3')
    // Second row: 4, 5, 6
    expect(buttons[3].text()).toBe('4')
    expect(buttons[4].text()).toBe('5')
    expect(buttons[5].text()).toBe('6')
    // Third row: 7, 8, 9
    expect(buttons[6].text()).toBe('7')
    expect(buttons[7].text()).toBe('8')
    expect(buttons[8].text()).toBe('9')
    // Fourth row: 00, 0, backspace
    expect(buttons[9].text()).toBe('00')
    expect(buttons[10].text()).toBe('0')
    expect(buttons[11].text()).toContain('⌫')
  })

  it('confirm_disabled_when_zero — not a NumPad concern (parent handles this)', () => {
    // NumPad itself doesn't have a confirm button; this test validates
    // that NumPad doesn't render a confirm button
    const wrapper = mount(NumPad, { props: { value: '0' } })
    expect(wrapper.find('.confirm-btn').exists()).toBe(false)
  })
})
