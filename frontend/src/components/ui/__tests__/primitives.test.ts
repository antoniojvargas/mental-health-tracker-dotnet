// @vitest-environment jsdom
/**
 * Tests de los tres componentes estáticos del kit: Skeleton, Logo y Tooltip.
 *
 * Juntos porque son pequeños y comparten la misma preocupación —qué se anuncia y
 * qué se decora— y separarlos en tres ficheros de veinte líneas cada uno no
 * compra nada.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { nextTick, type Component } from 'vue'

import Skeleton from '../Skeleton.vue'
import Logo from '../Logo.vue'
import Tooltip from '../Tooltip.vue'

const montados: VueWrapper[] = []

/**
 * Monta en el `body` de verdad. VTU crea un div aparte por defecto, así que sin
 * `attachTo` el componente existe pero no cuelga de `document` y las búsquedas
 * por selector no encuentran nada: una suite en verde que no comprueba nada.
 */
function montar(component: Component, opciones: Record<string, unknown> = {}) {
  const wrapper = mount(component, { attachTo: document.body, ...opciones })
  montados.push(wrapper)
  return wrapper
}

afterEach(() => {
  for (const wrapper of montados.splice(0)) wrapper.unmount()
  vi.useRealTimers()
  document.body.innerHTML = ''
})

describe('Skeleton', () => {
  it('es decoración: aria-hidden, para no leer bloques vacíos', () => {
    // Un esqueleto legible recita un montón de bloques sin contenido y encima
    // tapa el texto real que va a aparecer ahí. Lo que hay que anunciar es
    // "cargando", y lo dice la región que lo contiene.
    expect(montar(Skeleton).attributes('aria-hidden')).toBe('true')
  })

  it('pulsa, para que se lea como pendiente y no como bloque gris fijo', () => {
    expect(montar(Skeleton).classes()).toContain('animate-pulse')
  })

  it('acepta la altura como clase, para heredar la escala de espaciado', () => {
    const bloque = montar(Skeleton, { attrs: { class: 'h-4 w-32' } })

    // Un `height` en píxeles hechos a mano no sigue la escala de la app.
    expect(bloque.classes()).toContain('h-4')
  })

  it('cambia la forma del borde', () => {
    expect(montar(Skeleton, { props: { rounded: 'full' } }).classes()).toContain('rounded-full')
    expect(montar(Skeleton, { props: { rounded: 'lg' } }).classes()).toContain('rounded-lg')
  })
})

describe('Logo', () => {
  it('es SVG en línea, sin archivo externo', () => {
    // Un `.svg` importado como `<img>` llega al DOM por otra vía del CSS: no
    // hereda `currentColor` y hay que mantener dos archivos para dos temas.
    expect(montar(Logo).element.tagName.toLowerCase()).toBe('svg')
  })

  it('se pinta con currentColor y los tonos de la paleta', () => {
    const clases = montar(Logo).classes()

    expect(clases.length).toBeGreaterThan(0)
  })

  it('usa los tonos de la marca en sus formas', () => {
    const svg = montar(Logo)
    const circulos = svg.findAll('circle')

    // El anillo de `ink-300` y el punto de `clearsky-500`, los mismos que usa
    // el login. Un logo con otros tonos dejaría de ser el mismo símbolo.
    expect(circulos).toHaveLength(2)
    expect(circulos[0].classes()).toContain('text-ink-300')
    expect(circulos[1].classes()).toContain('text-clearsky-500')
  })

  it('el anillo tiene su hueco, que es lo que lo hace el símbolo', () => {
    expect(montar(Logo).findAll('circle')[0].attributes('stroke-dasharray')).toBe('66 22')
  })

  it('tiene nombre accesible por defecto', () => {
    const logo = montar(Logo)

    // Un `<svg>` suelto no lo anuncia un lector de pantalla: necesita un rol.
    expect(logo.attributes('role')).toBe('img')
    expect(logo.attributes('aria-label')).toBe('Stillwater')
  })

  it('puede quedar mudo, para usarlo como adorno', () => {
    const logo = montar(Logo, { props: { label: '' } })

    // El mismo componente sirve para el header y para la esquina de una tarjeta.
    expect(logo.attributes('role')).toBeUndefined()
    expect(logo.attributes('aria-hidden')).toBe('true')
  })

  it('cambia de tamaño', () => {
    expect(montar(Logo, { props: { size: 'sm' } }).classes()).toContain('h-8')
    expect(montar(Logo, { props: { size: 'lg' } }).classes()).toContain('h-14')
  })
})

describe('Tooltip', () => {
  const montarTooltip = (props: Record<string, unknown> = {}) =>
    montar(Tooltip, {
      props: { text: 'Guardar cambios', ...props },
      slots: { default: 'i' },
    })

  it('no se ve hasta que el disparador recibe atención', () => {
    montarTooltip()

    expect(document.querySelector('[role="tooltip"]')).toBeNull()
  })

  it('aparece al enfocar, no solo al pasar el ratón', async () => {
    // Un tooltip que solo sale con el ratón es un tooltip que no existe para
    // quien navega con el teclado, que es lo más común en un formulario.
    const wrapper = montarTooltip()

    await wrapper.get('button').trigger('focus')
    await nextTick()

    expect(document.querySelector('[role="tooltip"]')?.textContent).toContain('Guardar cambios')
  })

  it('el disparador se describe con el tooltip mientras está visible', async () => {
    const wrapper = montarTooltip()

    await wrapper.get('button').trigger('focus')
    await nextTick()

    const describedBy = wrapper.get('button').attributes('aria-describedby')
    // Un tooltip visible sin el enlace no se anuncia: se ve y no se oye.
    expect(describedBy).toBeTruthy()
    expect(document.getElementById(describedBy!)).not.toBeNull()
  })

  it('no describe nada cuando el tooltip está oculto', () => {
    // Un `aria-describedby` a un id inexistente es un enlace roto, y es peor que
    // no enlazar nada: el lector anuncia un nombre vacío.
    expect(montarTooltip().get('button').attributes('aria-describedby')).toBeUndefined()
  })

  it('desaparece al perder el foco', async () => {
    const wrapper = montarTooltip()

    await wrapper.get('button').trigger('focus')
    await nextTick()
    expect(document.querySelector('[role="tooltip"]')).not.toBeNull()

    await wrapper.get('button').trigger('blur')
    await nextTick()

    expect(document.querySelector('[role="tooltip"]')).toBeNull()
  })

  it('espera antes de abrirse con el ratón, para no molestar al pasar el cursor', async () => {
    vi.useFakeTimers()
    // El `mouseenter` va en el envoltorio, no en el botón: es el componente entero
    // el que entra y sale bajo el cursor.
    const wrapper = montarTooltip({ delay: 200 })
    const envoltorio = wrapper.element

    envoltorio.dispatchEvent(new MouseEvent('mouseenter'))
    await nextTick()
    expect(document.querySelector('[role="tooltip"]')).toBeNull()

    vi.advanceTimersByTime(200)
    await nextTick()

    expect(document.querySelector('[role="tooltip"]')).not.toBeNull()
  })

  it('el texto va suelto, sin aria-label encima', async () => {
    // Si el elemento lo nombrara con un `aria-label`, el lector anunciaría la
    // etiqueta en vez del texto visible, y aquí son la misma cosa.
    const wrapper = montarTooltip()

    await wrapper.get('button').trigger('focus')
    await nextTick()

    const tip = document.querySelector('[role="tooltip"]')!
    expect(tip.getAttribute('aria-label')).toBeNull()
    expect(tip.textContent!.trim()).toBe('Guardar cambios')
  })

  it('cierra con Escape', async () => {
    const wrapper = montarTooltip()

    await wrapper.get('button').trigger('focus')
    await nextTick()
    expect(document.querySelector('[role="tooltip"]')).not.toBeNull()

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    await nextTick()

    expect(document.querySelector('[role="tooltip"]')).toBeNull()
  })

  it('dos tooltips a la vez no comparten el id', async () => {
    // Con el mismo `id`, cada `aria-describedby` describiría el tooltip del otro.
    montarTooltip({ text: 'primero' })
    montarTooltip({ text: 'segundo' })

    const botones = document.querySelectorAll('button[aria-describedby]')
    expect(botones).toHaveLength(0)
  })
})
