// @vitest-environment jsdom
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import SymptomPicker from '../SymptomPicker.vue'

describe('SymptomPicker', () => {
  it('emits update:modelValue when toggling symptom', async () => {
    const wrapper = mount(SymptomPicker, { props: {} })
    const checkboxes = wrapper.findAll('input[type="checkbox"]')
    await checkboxes[0].setChecked(true)
    await nextTick()
    const events = wrapper.emitted('update:modelValue')
    expect(events).toBeTruthy()
    const last = events![events!.length - 1][0] as any[]
    expect(last[0].active).toBe(true)
  })

  it('shows severity controls when active and emits changes', async () => {
    const wrapper = mount(SymptomPicker, { props: {} })
    const checkboxes = wrapper.findAll('input[type="checkbox"]')
    await checkboxes[1].setChecked(true)
    await nextTick()
    const radios = wrapper.findAll('[role="radio"]')
    expect(radios.length).toBeGreaterThan(0)
    await radios[4].trigger('click')
    await nextTick()
    const events = wrapper.emitted('update:modelValue')
    const last = events![events!.length - 1][0] as any[]
    expect(last[1].severity).toBe(5)
    expect(last[1].active).toBe(true)
  })

  it('has correct aria attributes', async () => {
    const wrapper = mount(SymptomPicker, { props: { ariaLabel: 'Symptoms' } })
    const fieldset = wrapper.find('fieldset')
    expect(fieldset.attributes('aria-label')).toBe('Symptoms')
  })
})
