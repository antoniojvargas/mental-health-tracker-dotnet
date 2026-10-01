// @vitest-environment jsdom
/**
 * Tests de componente de Toast.
 *
 * Monta el contenedor real y lanza avisos de verdad con el composable, sin
 * empujar la lista a mano. Lo que se comprueba es la relación entre lo que
 * `useToast` publica y lo que el componente pinta y anuncia, y empujar la lista
 * desde el test se saltaría justo esa relación.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { nextTick } from 'vue'

import Toast from '../Toast.vue'
import { useToast } from '../../../composables/useToast'

/**
 * Avisos en pantalla, por su texto.
 *
 * Se busca en el `document` y no en el wrapper porque el componente no usa
 * `Teleport`: no lo necesita, no hay `overflow-hidden` que lo recorte, y el
 * `body` es su sitio natural. Aun así, el wrapper de VTU no cubre nodos que se
 * mueven al `body`, y leer de ahí es lo que hace quien usa la app de verdad.
 */
/**
 * Monta el contenedor en el `body` de verdad. VTU crea un div aparte por defecto,
 * así que sin `attachTo` el componente existe pero no cuelga de `document` y las
 * búsquedas por selector no encuentran nada —con tests verdes y sin comprobar
 * nada de lo que dicen comprobar.
 */
const montados: VueWrapper[] = []

function montar() {
  const wrapper = mount(Toast, { attachTo: document.body })
  montados.push(wrapper)
  return wrapper
}

function enPantalla() {
  return Array.from(document.querySelectorAll('p')).map((p) => p.textContent!.trim())
}

describe('Toast', () => {
  afterEach(() => {
    // Desmontar antes de limpiar: el estado de los avisos es un singleton, y
    // un contenedor anterior sin desmontar seguiría en el `body` con sus avisos
    // pintados, de modo que `querySelectorAll` los contaría como propios.
    for (const wrapper of montados.splice(0)) wrapper.unmount()
    useToast().clear()
    vi.useRealTimers()
    document.body.innerHTML = ''
  })

  describe('la región viva', () => {
    it('el contenedor es la región status, no cada aviso', () => {
      montar()

      const region = document.querySelector('[role="status"]')

      // Si el `role` estuviera en cada aviso, la región viva sería un nodo nuevo
      // en cada aparición, y se anunciaría al insertarse en vez de al cambiar:
      // el aviso se oye, pero cortando lo que el lector estuviera leyendo.
      expect(region).not.toBeNull()
      expect(region!.getAttribute('aria-live')).toBe('polite')
    })

    it('no es atómica, para no releer los avisos que ya estaban', () => {
      montar()

      // Con `aria-atomic="true"` en un contenedor apilado, cada aviso nuevo
      // vuelve a leer los anteriores, y un error repetido se oiría tres veces.
      expect(document.querySelector('[role="status"]')!.getAttribute('aria-atomic')).toBe('false')
    })

    it('los avisos no llevan su propio role, para no duplicar la región', async () => {
      montar()
      useToast().success('Guardado')
      await nextTick()

      // Solo debe haber una región viva: un `role="status"` por aviso además
      // de el del contenedor sería hablar dos veces de lo mismo.
      expect(document.querySelectorAll('[role="status"]')).toHaveLength(1)
    })
  })

  describe('qué se ve', () => {
    it('pinta el texto del aviso', async () => {
      montar()
      useToast().success('Guardado')
      await nextTick()

      expect(enPantalla()).toContain('Guardado')
    })

    it('distingue visualmente success de error por su borde', async () => {
      montar()
      useToast().success('Guardado')
      useToast().error('No se pudo guardar')
      await nextTick()

      // `meadow` es el acento de confirmación de la paleta y `ink` el texto. Un
      // aviso de error tiene que poder distinguirse de un ok sin leer el texto:
      // un usuario que solo percibe color necesita esa diferencia.
      const clases = Array.from(document.querySelectorAll('p')).map(
        (p) => p.parentElement!.className,
      )
      expect(clases.some((c) => c.includes('meadow'))).toBe(true)
      expect(clases.some((c) => c.includes('bg-ink-50'))).toBe(true)
    })

    it('el punto de color es decorativo, no el único aviso del tono', async () => {
      montar()
      useToast().error('No se pudo guardar')
      await nextTick()

      // Un lector de pantalla no deduce un error de un círculo. El significado
      // tiene que estar escrito, y el icono solo decora.
      const punto = document.querySelector('span[aria-hidden="true"]')
      expect(punto).not.toBeNull()
      expect(enPantalla()).toContain('No se pudo guardar')
    })

    it('no pinta nada cuando no hay avisos', () => {
      montar()

      expect(enPantalla()).toHaveLength(0)
    })
  })

  describe('cierre a mano', () => {
    it('el botón de cerrar nombra el aviso que descarta', async () => {
      montar()
      useToast().error('No se pudo guardar')
      await nextTick()

      const boton = document.querySelector('button')!

      // Con un `aria-label` de una palabra fija, tres errores apilados tendrían
      // tres botones idénticos y no se podría saber cuál cerrar.
      expect(boton.getAttribute('aria-label')).toBe('Descartar: No se pudo guardar')
    })

    it('descarta el aviso al pulsar el botón', async () => {
      montar()
      useToast().success('Guardado')
      await nextTick()

      document.querySelector<HTMLButtonElement>('button')!.click()
      await nextTick()

      expect(enPantalla()).not.toContain('Guardado')
    })

    it('descarta solo el aviso pulsado, no los demás', async () => {
      montar()
      useToast().success('primero')
      useToast().success('segundo')
      await nextTick()

      document.querySelector<HTMLButtonElement>('button')!.click()
      await nextTick()

      expect(enPantalla()).toContain('segundo')
    })
  })

  describe('desaparición automática', () => {
    it('el aviso se retira solo de la pantalla', async () => {
      vi.useFakeTimers()
      montar()
      useToast().success('Guardado')
      await nextTick()
      expect(enPantalla()).toContain('Guardado')

      vi.advanceTimersByTime(5000)
      await nextTick()

      expect(enPantalla()).not.toContain('Guardado')
    })
  })
})
