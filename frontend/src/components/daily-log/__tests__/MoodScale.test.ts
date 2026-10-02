// @vitest-environment jsdom
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import MoodScale from '../MoodScale.vue'

describe('MoodScale', () => {
  it('emits update:modelValue on click', async () => {
    const wrapper = mount(MoodScale, { props: { modelValue: null } })
    const radios = wrapper.findAll('[role="radio"]')
    await radios[2].trigger('click')
    await nextTick()
    expect(wrapper.emitted('update:modelValue')).toBeTruthy()
    const values = wrapper.emitted('update:modelValue') as number[][]
    expect(values[0][0]).toBe(3)
  })

  it('supports keyboard navigation with arrows and selection', async () => {
    const wrapper = mount(MoodScale, { props: { modelValue: null } })
    const group = wrapper.find('[role="radiogroup"]')
    await group.trigger('keydown', { key: 'ArrowRight' })
    await group.trigger('keydown', { key: 'Enter' })
    await nextTick()
    const values = wrapper.emitted('update:modelValue') as number[][]
    expect(values[0][0]).toBe(2)
  })

  it('sets correct aria-checked', async () => {
    const wrapper = mount(MoodScale, { props: { modelValue: 4 } })
    const radios = wrapper.findAll('[role="radio"]')
    expect(radios[3].attributes('aria-checked')).toBe('true')
    expect(radios[0].attributes('aria-checked')).toBe('false')
  })
})
