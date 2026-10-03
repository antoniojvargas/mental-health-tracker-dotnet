// @vitest-environment jsdom
import { describe, expect, it } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'

import RangeToggle from '../RangeToggle.vue'

function option(wrapper: VueWrapper, label: string) {
  return wrapper.findAll('button').find((button) => button.text() === label)!
}

describe('RangeToggle', () => {
  it('marca con aria-pressed la ventana activa', () => {
    const wrapper = mount(RangeToggle, { props: { modelValue: 'week' } })

    expect(option(wrapper, 'Week').attributes('aria-pressed')).toBe('true')
    expect(option(wrapper, 'Month').attributes('aria-pressed')).toBe('false')
  })

  it('emite la nueva ventana al pulsar la otra opción', async () => {
    const wrapper = mount(RangeToggle, { props: { modelValue: 'week' } })

    await option(wrapper, 'Month').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.at(-1)?.[0]).toBe('month')
  })

  it('mueve aria-pressed al cambiar la prop', async () => {
    const wrapper = mount(RangeToggle, { props: { modelValue: 'week' } })

    await wrapper.setProps({ modelValue: 'month' })

    expect(option(wrapper, 'Week').attributes('aria-pressed')).toBe('false')
    expect(option(wrapper, 'Month').attributes('aria-pressed')).toBe('true')
  })
})
