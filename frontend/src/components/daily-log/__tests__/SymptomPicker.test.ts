// @vitest-environment jsdom
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import SymptomPicker, { type SymptomEntry } from '../SymptomPicker.vue'

function defaultEntries(): SymptomEntry[] {
  return [
    { key: 'anxiety', label: 'Anxiety', active: false, severity: 1 },
    { key: 'depression', label: 'Depression', active: false, severity: 1 },
    { key: 'sleep', label: 'Sleep', active: false, severity: 1 },
    { key: 'appetite', label: 'Appetite', active: false, severity: 1 },
    { key: 'energy', label: 'Energy', active: false, severity: 1 },
    { key: 'concentration', label: 'Concentration', active: false, severity: 1 },
    { key: 'irritability', label: 'Irritability', active: false, severity: 1 },
    { key: 'pain', label: 'Pain', active: false, severity: 1 },
  ]
}

describe('SymptomPicker', () => {
  it('emits update:modelValue when toggling symptom', async () => {
    const wrapper = mount(SymptomPicker, { props: { modelValue: defaultEntries() } })
    await wrapper.findAll('input[type="checkbox"]')[0].setValue(true)
    await nextTick()
    const events = wrapper.emitted('update:modelValue')
    expect(events).toBeTruthy()
    const last = events![events!.length - 1][0] as SymptomEntry[]
    expect(last[0].active).toBe(true)
  })

  it('shows severity controls when active and emits changes', async () => {
    const wrapper = mount(SymptomPicker, {
      props: {
        modelValue: defaultEntries(),
        'onUpdate:modelValue': (value: SymptomEntry[]) => wrapper.setProps({ modelValue: value }),
      },
    })
    await wrapper.findAll('input[type="checkbox"]')[1].setValue(true)
    await nextTick()
    await nextTick()
    expect(wrapper.findAll('[role="radio"]').length).toBe(5)
    await wrapper.findAll('[role="radio"]')[4].trigger('click')
    await nextTick()
    const events = wrapper.emitted('update:modelValue')
    expect(events).toBeTruthy()
    const last = events![events!.length - 1][0] as SymptomEntry[]
    const target = last.find((x) => x.key === 'depression') ?? last[last.length - 1]
    expect(target.active).toBe(true)
  })

  it('has correct aria attributes', async () => {
    const wrapper = mount(SymptomPicker, { props: { ariaLabel: 'Symptoms' } })
    const fieldset = wrapper.find('fieldset')
    expect(fieldset.attributes('aria-label')).toBe('Symptoms')
  })
})
