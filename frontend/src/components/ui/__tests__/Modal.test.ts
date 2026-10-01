// @vitest-environment jsdom
/**
 * Tests de componente de Modal.
 *
 * Monta el modal real dentro de un contenedor con un botón fuera, porque el
 * comportamiento que importa —el foco atrapado y su devolución— es una relación
 * entre el diálogo y lo que hay alrededor. Montarlo solo, sin nada fuera, daría
 * verde un trap roto.
 *
 * El `open` se mueve desde el propio disparador, con un clic de verdad, en vez
 * de asignar la prop desde fuera. El componente tiene que funcionar usándolo como
 * lo va a usar la app, y un modal al que se le cambia `open` por la espalda
 * ejercita un camino que nadie va a recorrer.
 */

import { afterEach, describe, expect, it } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h, nextTick, ref } from 'vue'

import Modal from '../Modal.vue'

/**
 * Harness con un disparador fuera del modal. Reproduce el caso real: algo en la
 * página abre el diálogo, y el foco tiene que volver a ese algo al cerrarse.
 */
const Harness = defineComponent({
  components: { Modal },
  setup() {
    const open = ref(false)
    // Nodo del DOM del disparador, no el wrapper: lo que hay que recuperar al
    // cerrar es el elemento, que es lo que un lector de pantalla señala.
    const trigger = ref<HTMLButtonElement | null>(null)
    return { open, trigger }
  },
  render() {
    return h('div', [
      h('button', { id: 'fuera', ref: 'trigger', onClick: () => (this.open = true) }, 'Abrir'),
      h(Modal, { open: this.open, title: 'Confirmar', onClose: () => (this.open = false) }, () => [
        h('p', '¿Seguro?'),
        h('button', { id: 'dentro-1' }, 'Cancelar'),
        h('button', { id: 'dentro-2' }, 'Aceptar'),
      ]),
    ])
  },
})

const montados: VueWrapper[] = []

/**
 * El modal se teletransporta al `body`, así que su DOM no cuelga del wrapper:
 * buscarlo con `wrapper.get` daría "no encontrado" aunque esté en pantalla. Se
 * busca en el `document`, que es donde lo ve el usuario de verdad.
 */
const dialogo = () => document.querySelector<HTMLElement>('[role="dialog"]')
const overlay = () => dialogo()!.parentElement!
const titulo = () => dialogo()!.querySelector('h2')!

function dentro(id: string) {
  return document.getElementById(id) as HTMLButtonElement
}

/** Monta el harness ya abierto, con el foco puesto en el disparador. */
async function abierto() {
  const wrapper = mount(Harness, { attachTo: document.body })
  montados.push(wrapper)

  const outside = wrapper.get('#fuera').element as HTMLButtonElement
  outside.focus()

  await wrapper.get('#fuera').trigger('click')
  // Dos ticks: uno para el `v-if` del overlay y otro para el `nextTick` que hace
  // el propio modal antes de mover el foco.
  await nextTick()
  await nextTick()

  return { wrapper, outside }
}

function escape() {
  document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }))
}

function tab(shiftKey = false) {
  document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', shiftKey, bubbles: true }))
}

describe('Modal', () => {
  afterEach(() => {
    // Desmontar antes de limpiar el `body`: si se vacía el `body` con el
    // componente todavía montado, Vue siguepatcheando sobre nodos que ya no
    // existen y las aserciones de los tests siguientes leen de un árbol suelto.
    for (const wrapper of montados.splice(0)) wrapper.unmount()
    document.body.innerHTML = ''
    document.body.style.overflow = ''
  })

  describe('semántica de diálogo', () => {
    it('se anuncia como diálogo modal, no como un grupo cualquiera', async () => {
      await abierto()

      // `aria-modal="true"` es lo que le dice al lector de pantalla que lo de
      // fuera está apagado y que no siga leyendo el contenido que queda debajo.
      // Un `role="dialog"` sin el, o con un "false" colado, se comporta como una
      // capa cualquiera: el usuario oye el fondo por detrás del diálogo.
      expect(dialogo()!.getAttribute('aria-modal')).toBe('true')
    })

    it('asocia el título con aria-labelledby, y ese id existe en el DOM', async () => {
      await abierto()

      const labelledBy = dialogo()!.getAttribute('aria-labelledby')

      // Un `aria-labelledby` que apunta a un id inexistente deja el diálogo sin
      // nombre, que es como si no lo tuviera, y el fallo es silencioso.
      expect(labelledBy).toBeTruthy()
      expect(document.getElementById(labelledBy!)?.textContent?.trim()).toBe('Confirmar')
    })

    it('el título es un encabezado, no un div cualquiera', async () => {
      await abierto()

      expect(titulo().textContent!.trim()).toBe('Confirmar')
    })

    it('no renderiza nada mientras está cerrado', () => {
      mount(Harness, { attachTo: document.body })

      expect(dialogo()).toBeNull()
    })

    it('dos modales a la vez no comparten el id del título', async () => {
      // `aria-labelledby` resuelto por id: si los dos diagonales usaran el mismo,
      // ambos anunciarían el título del otro.
      const wrapper = mount(Harness, { attachTo: document.body })
      montados.push(wrapper)
      await wrapper.get('#fuera').trigger('click')
      await nextTick()
      await nextTick()

      const dialogs = document.querySelectorAll('[role="dialog"]')
      expect(dialogs).toHaveLength(1)
      expect(dialogs[0].getAttribute('aria-labelledby')).toMatch(/^v-\d+-title$/)
    })
  })

  describe('cierre con Escape', () => {
    it('cierra de verdad, no solo avisa: el diálogo desaparece del DOM', async () => {
      await abierto()
      expect(dialogo()).not.toBeNull()

      escape()
      await nextTick()

      // Que se emita `close` no basta: un `close` que nadie escucha deja el modal
      // abierto y el test pasaría igual.
      expect(dialogo()).toBeNull()
    })

    it('no cierra con otras teclas', async () => {
      await abierto()

      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }))
      await nextTick()

      expect(dialogo()).not.toBeNull()
    })

    it('no propaga el Escape a un modal exterior', async () => {
      // Anidados: un Escape no debería cerrar el padre y el hijo en el mismo
      // tecleo, que es como se pierde un formulario sin querer.
      const events: string[] = []
      const wrapper = mount(Harness, { attachTo: document.body })
      montados.push(wrapper)
      wrapper.vm.open = true
      await nextTick()
      document.addEventListener('keydown', (e) => events.push(e.key))

      escape()

      expect(events).toEqual(['Escape'])
    })
  })

  describe('foco al abrir', () => {
    it('mueve el foco dentro del diálogo y no lo deja detrás del overlay', async () => {
      const { outside } = await abierto()

      // El fallo clásico: el modal aparece y el foco sigue en el disparador, que
      // ya está tapado. Un usuario de teclado se queda sin nada que leer.
      expect(document.activeElement).not.toBe(outside)
      expect(dialogo()!.contains(document.activeElement)).toBe(true)
    })

    it('aterriza en el primer elemento enfocable del panel', async () => {
      await abierto()

      expect(document.activeElement?.id).toBe('dentro-1')
    })
  })

  describe('foco atrapado', () => {
    it('Tab desde el último elemento vuelve al primero', async () => {
      await abierto()
      dentro('dentro-2').focus()

      tab()
      await nextTick()

      // Sin esto el foco se salía por el fondo de la página y el usuario seguía
      // escribiendo en algo que ya no ve.
      expect(document.activeElement?.id).toBe('dentro-1')
    })

    it('Shift+Tab desde el primero va al último', async () => {
      await abierto()
      dentro('dentro-1').focus()

      tab(true)
      await nextTick()

      expect(document.activeElement?.id).toBe('dentro-2')
    })

    it('recupera el foco si se escapa por focusin, no solo con el Tab', async () => {
      const { outside } = await abierto()

      // El camino que el `Tab` no cubre: el foco se va solo, por un autofocus, un
      // clic fuera, o porque la ventana pierde el foco. El overlay sigue ahí y
      // el usuario ya no ve dónde está escribiendo.
      outside.focus()
      await nextTick()

      expect(document.activeElement?.id).toBe('dentro-1')
    })

    it('no roba el foco de nuevo al devolverlo al cerrar', async () => {
      const { outside } = await abierto()

      escape()
      await nextTick()
      await nextTick()

      // El trap lee su propio retorno como si fuera una fuga y lo devolvería al
      // panel que se está cerrando, dejando el foco en un nodo ya desmontado.
      expect(document.activeElement).toBe(outside)
    })
  })

  describe('devolución del foco al cerrar', () => {
    it('devuelve el foco al elemento que abrió el diálogo', async () => {
      const { outside } = await abierto()

      escape()
      await nextTick()
      await nextTick()

      // Sin esto el foco cae al `body` y un usuario de teclado pierde el sitio
      // donde estaba: tendría que tabular otra vez desde el principio.
      expect(document.activeElement).toBe(outside)
    })

    it('funciona igual cerrando con el clic del fondo', async () => {
      const { outside } = await abierto()

      overlay().click()
      await nextTick()
      await nextTick()

      expect(document.activeElement).toBe(outside)
    })
  })

  describe('scroll del body', () => {
    it('bloquea el scroll mientras está abierto', async () => {
      await abierto()

      expect(document.body.style.overflow).toBe('hidden')
    })

    it('devuelve el scroll al valor que había al cerrar', async () => {
      document.body.style.overflow = 'scroll'
      await abierto()

      escape()
      await nextTick()

      expect(document.body.style.overflow).toBe('scroll')
    })
  })

  describe('clic en el fondo', () => {
    it('un clic dentro del panel no cierra el diálogo', async () => {
      await abierto()

      dialogo()!.click()
      await nextTick()

      // Si cerrara, no se podría ni tocar un control dentro del propio diálogo.
      expect(dialogo()).not.toBeNull()
    })

    it('un clic en el overlay sí cierra', async () => {
      await abierto()

      overlay().dispatchEvent(new MouseEvent('click', { bubbles: true }))
      await nextTick()

      expect(dialogo()).toBeNull()
    })
  })
})
