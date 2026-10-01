/**
 * Tests de `useToast`.
 *
 * Corre en `node`: el composable no toca el DOM, solo `setTimeout` y un `ref`.
 * El DOM es cosa del componente, que tiene su propio fichero.
 *
 * El estado es un singleton a nivel de módulo, así que cada test tiene que
 * limpiarlo. `clear()` hace las dos cosas —vaciar la lista y cancelar los
 * temporizadores— y sin ella un `setTimeout` de un test anterior revienta en el
 * siguiente, dentro de otro test que no tiene nada que ver.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { useToast } from '../useToast'

describe('useToast', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    // `clear()` antes de `useRealTimers()`: cancelar un temporizador de reloj
    // falso y luego restaurar el real deja avisos colgados.
    useToast().clear()
    vi.useRealTimers()
  })

  describe('lanzar avisos', () => {
    it('añade un success con su tono y su texto', () => {
      const { success, toasts } = useToast()

      success('Guardado')

      expect(toasts.value).toHaveLength(1)
      expect(toasts.value[0]).toMatchObject({ tone: 'success', message: 'Guardado' })
    })

    it('añade un error con su tono y su texto', () => {
      const { error, toasts } = useToast()

      error('No se pudo guardar')

      expect(toasts.value).toHaveLength(1)
      expect(toasts.value[0]).toMatchObject({ tone: 'error', message: 'No se pudo guardar' })
    })

    it('da un id distinto a cada aviso, para no confundirlos al descartar', () => {
      const { success, toasts } = useToast()

      success('uno')
      success('dos')

      expect(toasts.value[0].id).not.toBe(toasts.value[1].id)
    })

    it('apila en orden, y el más reciente al final', () => {
      const { success, toasts } = useToast()

      success('primero')
      success('segundo')

      expect(toasts.value.map((t) => t.message)).toEqual(['primero', 'segundo'])
    })

    it('devuelve el id del aviso creado', () => {
      // Sin esto, quien lanza el aviso no podría descartarlo a mano.
      const { success } = useToast()

      expect(success('Guardado')).toBeTypeOf('number')
    })
  })

  describe('desaparición automática', () => {
    it('retira un success a los 5s', () => {
      const { success, toasts } = useToast()
      success('Guardado')

      vi.advanceTimersByTime(4999)
      expect(toasts.value).toHaveLength(1)

      vi.advanceTimersByTime(1)
      // El aviso se va solo: es lo que lo distingue de un error que se queda
      // pegado molestando en una esquina.
      expect(toasts.value).toHaveLength(0)
    })

    it('retira un error a los 8s, más tarde que un success', () => {
      const { error, toasts } = useToast()
      error('No se pudo guardar')

      vi.advanceTimersByTime(5000)
      // Un fallo necesita más tiempo para ser leído que un ok. Si durara lo
      // mismo, se perderían en la misma cola y el error, que es lo que importa,
      // sería el que se va primero.
      expect(toasts.value).toHaveLength(1)

      vi.advanceTimersByTime(3000)
      expect(toasts.value).toHaveLength(0)
    })

    it('acepta una duración propia, que manda sobre la de por defecto', () => {
      const { success, toasts } = useToast()
      success('Guardado', 1000)

      vi.advanceTimersByTime(1000)

      expect(toasts.value).toHaveLength(0)
    })

    it('con duration 0 el aviso se queda hasta que se descarte a mano', () => {
      const { error, toasts } = useToast()
      error('Sesión caducada', 0)

      vi.advanceTimersByTime(60000)

      // Un fallo de sesión no puede desaparecer solo: si se va, el usuario se
      // queda sin saber por qué le ha cambiado todo de golpe.
      expect(toasts.value).toHaveLength(1)
    })

    it('descarta a mano y no reaparece después', () => {
      const { error, toasts, dismiss } = useToast()
      const id = error('No se pudo guardar', 5000)

      dismiss(id)
      expect(toasts.value).toHaveLength(0)

      vi.advanceTimersByTime(5000)
      // El fallo clásico: el temporizador sigue vivo tras descartar, y el aviso
      // reaparece segundos después como si se hubiera deshecho solo.
      expect(toasts.value).toHaveLength(0)
    })

    it('descartar cancela el temporizador, que si no queda vivo', () => {
      const { success, dismiss } = useToast()
      const id = success('Guardado')
      expect(vi.getTimerCount()).toBe(1)

      dismiss(id)

      // Un temporizador huérfano no se nota en la lista —el aviso ya está
      // fuera— pero sigue despertando al navegador cinco segundos después para
      // no hacer nada. Con muchos avisos descartados a mano, son cinco segundos
      // de trabajo en cada uno.
      expect(vi.getTimerCount()).toBe(0)
    })

    it('descartar dos veces no rompe nada', () => {
      const { error, toasts, dismiss } = useToast()
      const id = error('No se pudo guardar', 0)

      dismiss(id)
      dismiss(id)

      expect(toasts.value).toHaveLength(0)
    })

    it('descartar un aviso no toca los demás', () => {
      const { success, toasts, dismiss } = useToast()
      const id = success('primero')
      success('segundo')

      dismiss(id)

      expect(toasts.value.map((t) => t.message)).toEqual(['segundo'])
    })
  })

  describe('tope de avisos a la vez', () => {
    it('no deja más de tres visibles, que es lo que cabe sin tapar la pantalla', () => {
      const { success, toasts } = useToast()

      for (const m of ['uno', 'dos', 'tres', 'cuatro', 'cinco']) success(m)

      // Sin tope, un bucle que falla en cada iteración apila veinte avisos y
      // tapa la pantalla entera, que es lo contrario de lo que debe hacer un aviso.
      expect(toasts.value).toHaveLength(3)
    })

    it('retira el más antiguo y conserva los recientes', () => {
      const { success, toasts } = useToast()
      for (const m of ['uno', 'dos', 'tres', 'cuatro']) success(m)

      // El aviso nuevo suele importar más que el viejo, que ya ha pasado su rato.
      expect(toasts.value.map((t) => t.message)).toEqual(['dos', 'tres', 'cuatro'])
    })

    it('un error no expulsa a un success más reciente solo por ser error', () => {
      const { success, error, toasts } = useToast()
      for (const m of ['uno', 'dos', 'tres']) success(m)

      error('fallo')
      expect(toasts.value.map((t) => t.message)).toEqual(['dos', 'tres', 'fallo'])
    })
  })

  describe('clear', () => {
    it('vacía la lista', () => {
      const { success, toasts, clear } = useToast()
      success('uno')
      success('dos')

      clear()

      expect(toasts.value).toHaveLength(0)
    })

    it('deja la lista utilizable después de vaciarla', () => {
      // Los ids siguen corriendo a propósito: si `nextId` se reiniciara, un
      // temporizador viejo pendiente podría matar un aviso nuevo con el mismo id.
      const { success, toasts, clear } = useToast()
      success('uno')
      clear()

      success('otro')

      expect(toasts.value.map((t) => t.message)).toEqual(['otro'])
    })
  })

  describe('la lista es de solo lectura', () => {
    it('una escritura desde fuera no cambia la lista', () => {
      const { success, toasts } = useToast()
      success('Guardado')

      // Si el composable entregara el `ref` mutable, cualquier vista podría
      // meterse en la lista y descolocar avisos que otro componente está
      // contando para decidir cuándo animarlos fuera.
      //
      // Vue no lanza aquí: avisa por consola y deja el valor intacto. Por eso se
      // comprueba el efecto —que la lista no cambia— y no un `toThrow`, que
      // daría verde con un `readonly` que no hace nada.
      const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
      // El doble `as unknown as` es porque `readonly` ya lo marca el
      // compilador: el test forzaría la barrera para poder comprobar que en
      // tiempo de ejecución la lista tampoco cambia.
      ;(toasts as unknown as { value: unknown[] }).value = []
      warn.mockRestore()

      expect(toasts.value).toHaveLength(1)
      expect(toasts.value[0].message).toBe('Guardado')
    })

    it('tampoco se puede mutar la lista en la mano', () => {
      const { success, toasts } = useToast()
      success('Guardado')

      const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
      // Igual que arriba: `push` no existe en un array `readonly`, y el objeto
      // es el que este test intenta colar.
      ;(toasts.value as unknown as unknown[]).push({
        id: 999,
        tone: 'success',
        message: 'colado',
        duration: 0,
      })
      warn.mockRestore()

      expect(toasts.value.map((t) => t.message)).toEqual(['Guardado'])
    })
  })
})
