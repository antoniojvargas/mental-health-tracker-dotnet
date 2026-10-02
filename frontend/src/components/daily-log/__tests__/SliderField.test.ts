// @vitest-environment jsdom
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import SliderField from '../SliderField.vue'

describe('SliderField', () => {
  it('emits update:modelValue on input', async () => {
    const wrapper = mount(SliderField, {
      props: { label: 'Test', modelValue: 5, min: 0, max: 10 },
    })
    const input = wrapper.find('input[type="range"]')
    await input.setValue(8)
    await nextTick()
    const values = wrapper.emitted('update:modelValue') as number[][]
    expect(values[0][0]).toBe(8)
  })

  it('renders label and value text', () => {
    const wrapper = mount(SliderField, {
      props: { label: 'Anxiety', modelValue: 7, valueText: '7 - High' },
    })
    expect(wrapper.text()).toContain('Anxiety')
    expect(wrapper.text()).toContain('7 - High')
  })

  it('is accessible with aria-valuetext', () => {
    const wrapper = mount(SliderField, {
      props: { label: 'Sleep', modelValue: 3, valueText: 'Moderate' },
    })
    const input = wrapper.find('input[type="range"]')
    expect(input.attributes('aria-valuetext')).toBe('Moderate')
  })
})
