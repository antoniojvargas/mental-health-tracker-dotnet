// @vitest-environment jsdom
/**
 * Tests de componente de Button.
 *
 * Monta el botón real. Lo que se comprueba es mayormente semántica —qué
 * atributos acaba teniendo en el DOM— y eso solo se puede ver montando, porque
 * `disabled` y `aria-busy` en un `<button>` son atributos de verdad y no
 * propiedades que se puedan inspeccionar en un objeto.
 */

import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'

import Button from '../Button.vue'

/** Monta el botón con props y texto, y devuelve el `<button>` del DOM. */
function render(props: Record<string, unknown> = {}, text = 'Guardar') {
  return mount(Button, { props, slots: { default: text } })
}

describe('Button', () => {
  describe('variantes', () => {
    it('es primary por defecto, la acción principal de la pantalla', () => {
      // Una vista con dos botones primary no tiene un principal, y es un fallo
      // que se cuela hasta la revisión de diseño.
      expect(render().classes()).toContain('bg-clearsky-600')
    })

    it('subtle lleva caja propia, para una acción secundaria con peso', () => {
      const classes = render({ variant: 'subtle' }).classes()

      expect(classes).toContain('border-ink-200')
      expect(classes).toContain('bg-paper-50')
    })

    it('ghost no lleva caja: solo texto, para lo terciario', () => {
      const classes = render({ variant: 'ghost' }).classes()

      expect(classes).toContain('bg-transparent')
      expect(classes).toContain('border-transparent')
    })

    it('comparte el anillo de foco en las tres variantes', () => {
      // La variante cambia el relleno, nunca la señal de foco: si el teclado
      // leyera distinto según el botón, la navegación por tabulador dejaría de
      // ser predecible justo donde más importa.
      for (const variant of ['primary', 'ghost', 'subtle'] as const) {
        expect(render({ variant }).classes()).toContain('focus-visible:outline-clearsky-500')
      }
    })
  })

  describe('estado disabled', () => {
    it('deshabilita el botón nativo, no solo lo pinta', () => {
      // Un `disabled` de mentira con un manejador de clic detrás deja pulsar un
      // botón que dice que no se puede pulsar.
      expect(render({ disabled: true }).attributes('disabled')).toBeDefined()
    })

    it('no dispara el clic mientras está deshabilitado', async () => {
      const wrapper = mount(Button, {
        props: { disabled: true, onClick: () => void 0 },
        slots: { default: 'Guardar' },
      })

      await wrapper.trigger('click')

      expect(wrapper.emitted('click')).toBeUndefined()
    })

    it('baja la opacidad para que se lea como apagado y no como roto', () => {
      // Sigue siendo legible a propósito: un `disabled` que desaparece del
      // panel hace pensar que no hay nada ahí.
      expect(render({ disabled: true }).classes()).toContain('opacity-60')
    })
  })

  describe('estado loading', () => {
    it('marca aria-busy para que un lector de pantalla lo anuncie', () => {
      // `disabled` solo no dice nada: el usuario oye un botón que se apaga y no
      // entiende por qué. `aria-busy` es lo que explica la espera.
      expect(render({ loading: true }).attributes('aria-busy')).toBe('true')
    })

    it('sigue mostrando su etiqueta mientras carga', () => {
      // Sustituir el texto por el spinner haría saltar el ancho del botón al
      // terminar, y el texto es lo que dice qué está pasando.
      const wrapper = render({ loading: true })

      expect(wrapper.text()).toContain('Guardar')
      expect(wrapper.find('svg').exists()).toBe(true)
    })

    it('deshabilita el botón mientras carga, para no poder enviar dos veces', () => {
      expect(render({ loading: true }).attributes('disabled')).toBeDefined()
    })

    it('no deja el spinner en pantalla cuando ya no carga', () => {
      // Con `v-show` en vez de `v-if` el SVG seguiría en el DOM, oculto: se
      // seguiría anunciando y seguiría ocupando sitio al medir el botón.
      expect(render({ loading: false }).find('svg').exists()).toBe(false)
    })

    it('el spinner es decorativo y no se anuncia por separado', () => {
      // Si el SVG se leyera, el lector de pantalla recitaría "imagen" entre la
      // etiqueta y nada más.
      expect(render({ loading: true }).get('svg').attributes('aria-hidden')).toBe('true')
    })

    it('no deja aria-busy puesto cuando ya no carga', () => {
      expect(render({ loading: false }).attributes('aria-busy')).toBe('false')
    })
  })

  describe('comportamiento de formulario', () => {
    it('es type="button" por defecto, para no enviar un formulario por accidente', () => {
      // Dentro de un `<form>`, un `<button>` sin tipo se envía al pulsar Enter
      // en otro campo. Un botón que solo navega no debería mandar el formulario.
      expect(render().attributes('type')).toBe('button')
    })

    it('acepta type="submit" cuando sí tiene que enviar', () => {
      expect(render({ type: 'submit' }).attributes('type')).toBe('submit')
    })
  })
})
