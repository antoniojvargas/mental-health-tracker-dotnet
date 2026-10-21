// @vitest-environment jsdom
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import SymptomPicker from '../SymptomPicker.vue'
import { SYMPTOM_OPTIONS, createDefaultSymptoms, type SymptomEntry } from '../daily-log-options'
import { SYMPTOM_TYPES } from '../../../types/daily-log'

function defaultEntries(): SymptomEntry[] {
  return createDefaultSymptoms()
}

describe('SymptomPicker', () => {
  it('offers exactly the 8 SymptomType values with their labels', () => {
    const wrapper = mount(SymptomPicker)
    const labels = wrapper.findAll('label').map((l) => l.text())
    expect(labels).toEqual(SYMPTOM_OPTIONS.map((o) => o.label))
    expect(
      createDefaultSymptoms()
        .map((e) => e.key)
        .sort(),
    ).toEqual([...SYMPTOM_TYPES].sort())
  })

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
    const target = last.find((x) => x.key === 'low_mood') ?? last[last.length - 1]
    expect(target.active).toBe(true)
  })

  it('has correct aria attributes', async () => {
    const wrapper = mount(SymptomPicker, { props: { ariaLabel: 'Symptoms' } })
    const fieldset = wrapper.find('fieldset')
    expect(fieldset.attributes('aria-label')).toBe('Symptoms')
  })
})
