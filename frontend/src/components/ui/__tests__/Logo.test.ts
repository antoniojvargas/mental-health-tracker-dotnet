// @vitest-environment jsdom
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'

import Logo from '../Logo.vue'

describe('Logo', () => {
  it('con nombre es una imagen anunciada: role img y aria-label, sin aria-hidden', () => {
    const svg = mount(Logo, { props: { label: 'Stillwater' } }).get('svg')

    expect(svg.attributes('role')).toBe('img')
    expect(svg.attributes('aria-label')).toBe('Stillwater')
    expect(svg.attributes()).not.toHaveProperty('aria-hidden')
  })

  it('se llama Stillwater por defecto', () => {
    expect(mount(Logo).get('svg').attributes('aria-label')).toBe('Stillwater')
  })

  it('con etiqueta vacía es decorativo: aria-hidden y ni rol ni aria-label (ni siquiera vacío)', () => {
    const svg = mount(Logo, { props: { label: '' } }).get('svg')

    expect(svg.attributes('aria-hidden')).toBe('true')
    expect(svg.attributes()).not.toHaveProperty('role')
    expect(svg.attributes()).not.toHaveProperty('aria-label')
  })

  it.each([
    ['sm', 'h-8 w-8'],
    ['md', 'h-10 w-10'],
    ['lg', 'h-14 w-14'],
  ] as const)('el tamaño %s pone la clase %s', (size, classes) => {
    expect(mount(Logo, { props: { size } }).get('svg').attributes('class')).toBe(classes)
  })
})
