// @vitest-environment jsdom
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent, nextTick } from 'vue'
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

  describe('el id que enlaza etiqueta e input', () => {
    it('la etiqueta apunta al input por for/id', () => {
      const wrapper = mount(SliderField, { props: { label: 'Stress', modelValue: 4 } })

      const id = wrapper.get('input[type="range"]').attributes('id')

      expect(id).toBeTruthy()
      expect(wrapper.get('label').attributes('for')).toBe(id)
      expect(wrapper.get('output').attributes('for')).toBe(id)
    })

    it('cada instancia tiene el suyo, y no cambia al re-renderizar', async () => {
      // `useId` numera por aplicación: dos sliders de la misma pantalla, no de dos `mount()`.
      const Both = defineComponent({
        components: { SliderField },
        props: { first: { type: Number, default: 1 } },
        template: `<div><SliderField label="A" :model-value="first" /><SliderField label="B" :model-value="2" /></div>`,
      })
      const wrapper = mount(Both)
      const [firstId, secondId] = wrapper.findAll('input').map((i) => i.attributes('id'))

      expect(firstId).toBeTruthy()
      expect(firstId).not.toBe(secondId)

      await wrapper.setProps({ first: 9 })

      expect(wrapper.findAll('input')[0].attributes('id')).toBe(firstId)
    })

    it('respeta el id que se le pasa', () => {
      const wrapper = mount(SliderField, {
        props: { label: 'Mood', modelValue: 3, id: 'mi-slider' },
      })

      expect(wrapper.get('input').attributes('id')).toBe('mi-slider')
      expect(wrapper.get('label').attributes('for')).toBe('mi-slider')
    })
  })
})
