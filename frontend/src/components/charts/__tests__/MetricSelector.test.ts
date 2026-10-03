// @vitest-environment jsdom
import { describe, expect, it } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'

import MetricSelector from '../MetricSelector.vue'

function chip(wrapper: VueWrapper, label: string) {
  return wrapper.findAll('button').find((button) => button.text().includes(label))!
}

describe('MetricSelector', () => {
  it('marca como pulsadas solo las métricas seleccionadas', () => {
    const wrapper = mount(MetricSelector, { props: { modelValue: ['mood', 'anxiety'] } })

    expect(chip(wrapper, 'Mood').attributes('aria-pressed')).toBe('true')
    expect(chip(wrapper, 'Anxiety').attributes('aria-pressed')).toBe('true')
    expect(chip(wrapper, 'Stress').attributes('aria-pressed')).toBe('false')
  })

  it('añade una métrica y emite la selección nueva', async () => {
    const wrapper = mount(MetricSelector, { props: { modelValue: ['mood'] } })

    await chip(wrapper, 'Anxiety').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.at(-1)?.[0]).toEqual(['mood', 'anxiety'])
  })

  it('quita una métrica ya seleccionada al volver a pulsarla', async () => {
    const wrapper = mount(MetricSelector, { props: { modelValue: ['mood', 'anxiety'] } })

    await chip(wrapper, 'Mood').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.at(-1)?.[0]).toEqual(['anxiety'])
  })

  it('deshabilita las no seleccionadas al llegar a tres', () => {
    const wrapper = mount(MetricSelector, {
      props: { modelValue: ['mood', 'anxiety', 'stress'] },
    })

    expect(chip(wrapper, 'Hours of sleep').attributes('disabled')).toBeDefined()
  })

  it('deja quitar una de las tres seleccionadas aunque el tope esté lleno', async () => {
    const wrapper = mount(MetricSelector, {
      props: { modelValue: ['mood', 'anxiety', 'stress'] },
    })

    const stress = chip(wrapper, 'Stress')
    expect(stress.attributes('disabled')).toBeUndefined()

    await stress.trigger('click')

    expect(wrapper.emitted('update:modelValue')?.at(-1)?.[0]).toEqual(['mood', 'anxiety'])
  })

  it('no emite nada si se intenta añadir una cuarta', async () => {
    const wrapper = mount(MetricSelector, {
      props: { modelValue: ['mood', 'anxiety', 'stress'] },
    })

    await chip(wrapper, 'Hours of sleep').trigger('click')

    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
  })
})
