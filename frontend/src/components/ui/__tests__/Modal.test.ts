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

/**
 * Dos modales a la vez, abiertos en orden: el primero y el segundo.
 *
 * Hermanos, y no anidados, a propósito. Anidados, el segundo viviría en el slot
 * del primero, así que cerrando el de abajo se desmontaría el de encima con él y
 * no se podría observar nunca el cierre fuera de orden, que es justo lo que rompe
 * un `pop()` a ciegas. Aquí cada uno es independiente y puede cerrarse cuando
 * quiera, que es como se abre un modal desde una vista y otro desde un menú.
 *
 * Los dos se teletransportan al `body`, así que ninguno cuelga del panel del
 * otro: cada trap de foco ve el foco del otro como una fuga, y se lo quitan el
 * uno al otro en bucle.
 */
const DosModales = defineComponent({
  components: { Modal },
  setup() {
    return { fuera: ref(false), dentro: ref(false) }
  },
  render() {
    return h('div', [
      h('button', { id: 'abre-padre', onClick: () => (this.fuera = true) }, 'Abrir padre'),
      h(Modal, { open: this.fuera, title: 'Padre', onClose: () => (this.fuera = false) }, () => [
        h('p', { id: 'texto-del-padre' }, 'El padre no tiene nada enfocable'),
      ]),
      h('button', { id: 'abre-hijo', onClick: () => (this.dentro = true) }, 'Abrir hijo'),
      h(Modal, { open: this.dentro, title: 'Hijo', onClose: () => (this.dentro = false) }, () => [
        h('button', { id: 'hoja' }, 'Hoja'),
        h('button', { id: 'ultimo-del-hijo' }, 'Último del hijo'),
      ]),
    ])
  },
})

const dialogos = () => [...document.querySelectorAll('[role="dialog"]')]
const titulos = () => dialogos().map((d) => d.querySelector('h2')?.textContent)

/** Monta el harness y abre los dos modales, el segundo encima del primero. */
async function losDos() {
  const wrapper = mount(DosModales, { attachTo: document.body })
  montados.push(wrapper)
  // Clic sobre el nodo del `document`, no `wrapper.get`: los paneles están
  // teletransportados al `body` y no cuelgan del wrapper.
  document.getElementById('abre-padre')!.click()
  await nextTick()
  await nextTick()
  document.getElementById('abre-hijo')!.click()
  await nextTick()
  await nextTick()
  return wrapper
}

/**
 * Abre los dos y cierra el de abajo desde fuera, con el de encima todavía en
 * pantalla. Es el orden que rompe un `pop()` a ciegas: la entrada que toca
 * quitar es la de abajo, que no es la última de la pila.
 */
async function abajoCerradoPorFuera() {
  const wrapper = await losDos()
  wrapper.vm.fuera = false
  await nextTick()
  await nextTick()
  return wrapper
}

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
      // `aria-labelledby` resuelto por id: si los dos usan el mismo,
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

    it('con dos modales abiertos cierra solo el de encima', async () => {
      await losDos()
      expect(titulos()).toEqual(['Padre', 'Hijo'])

      escape()
      await nextTick()

      // El fallo que esto destapa: los dos modales están escuchando en el mismo
      // `document`, así que los dos ven el tecleo, y un solo Escape se llevaba
      // los dos a la vez, con ellos el formulario que había detrás.
      expect(titulos()).toEqual(['Padre'])
    })

    it('el segundo Escape sí cierra el de debajo', async () => {
      await losDos()

      escape()
      await nextTick()
      escape()
      await nextTick()

      // Si el cierre del de encima no lo saca de la pila, el de debajo sigue
      // creyendo que hay un modal encima y este segundo Escape no cerraría nada.
      expect(titulos()).toEqual([])
    })

    it('el Tab lo gestiona el modal de encima, no el de debajo', async () => {
      await losDos()
      document.getElementById('ultimo-del-hijo')!.focus()

      tab()
      await nextTick()

      // Con el foco en el último botón del de encima, el `Tab` tiene que dar la
      // vuelta dentro de ese modal. Aquí el de debajo no tiene nada enfocable,
      // que es el peor caso para él: su handler cae en la rama de "no hay a
      // dónde ir" y le focusearía su propio panel, llevándose el foco de un
      // diálogo que sigue encima. El usuario acabaría escribiendo en el que ya
      // no ve.
      expect(document.activeElement?.id).toBe('hoja')
      expect(dialogos()[1].contains(document.activeElement)).toBe(true)
    })

    it('el Shift+Tab del de encima tampoco cae en el de debajo', async () => {
      await losDos()
      document.getElementById('hoja')!.focus()

      tab(true)
      await nextTick()

      expect(document.activeElement?.id).toBe('ultimo-del-hijo')
    })

    it('el de debajo tampoco roba el foco al de encima', async () => {
      await losDos()

      // Los dos se teletransportan al `body`, así que ninguno está dentro del
      // panel del otro. Cada trap veía el foco del otro como una fuga y lo
      // recuperaba, en bucle, hasta reventar la pila de llamadas.
      expect(document.activeElement?.id).toBe('hoja')
    })

    it('el de debajo vuelve a atrapar el foco cuando el otro ya no está', async () => {
      await losDos()
      escape()
      await nextTick()
      await nextTick()
      document.getElementById('texto-del-padre')!.focus()

      tab()
      await nextTick()

      // Ya no hay nada encima, así que le toca a él devolver el foco a su panel.
      // Si no recuperara su turno, el foco escaparía hacia la página de detrás
      // con el overlay todavía visible.
      expect(dialogos()).toHaveLength(1)
      expect(dialogos()[0].contains(document.activeElement)).toBe(true)
    })

    it('el de encima sigue respondiendo si el de debajo se cierra desde fuera', async () => {
      await abajoCerradoPorFuera()
      // Ambos viven en el `body`, así que el de encima sobrevive al cierre del
      // otro.
      expect(titulos()).toEqual(['Hijo'])

      escape()
      await nextTick()

      // Si al cerrar el de abajo se hubiera llevado la entrada del de encima en
      // vez de la suya, la pila apuntaría a un modal ya desmontado y el otro
      // dejaría de cerrarse con Escape: un overlay con su disparador muerto.
      expect(titulos()).toEqual([])
    })

    it('restaura el scroll original si el de debajo se cierra primero', async () => {
      document.body.style.overflow = 'scroll'
      const wrapper = await abajoCerradoPorFuera()

      escape()
      await nextTick()
      wrapper.vm.dentro = false
      await nextTick()
      await nextTick()

      // Con el de abajo cerrándose primero, un `lockScroll`/`unlockScroll` que no
      // mire la pila guarda 'hidden' como valor anterior y lo devuelve como si
      // fuera el scroll real: la página se queda bloqueada para siempre.
      expect(document.body.style.overflow).toBe('scroll')
    })

    it('mantiene el scroll bloqueado con un modal cerrado encima', async () => {
      await losDos()

      escape()
      await nextTick()

      // Queda uno abierto: devolver el scroll aquí dejaría la página desplazable
      // por detrás de un overlay que sigue cubriendo la pantalla.
      expect(document.body.style.overflow).toBe('hidden')
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
