import { readonly, ref } from 'vue'

/**
 * Avisos efímeros: un `success` para confirmar y un `error` para avisar de un
 * fallo, sin robarle la pantalla a quien está trabajando.
 *
 * El estado vive a nivel de módulo y no dentro de un componente. Es lo que
 * permite que un servicio o un `catch` en medio de un cliente HTTP lance un aviso
 * sin tener que pasar el `emit` de tres capas hacia arriba, que es la razón por la
 * que un `error` acaba en la consola y el usuario nunca se entera.
 *
 * Un singleton también significa un solo temporizador vivo. Con estado por
 * componente, dos vistas montadas a la vez timers duplicados que se pisan entre
 * ellos al desmontar.
 */

export type ToastTone = 'success' | 'error'

export interface Toast {
  id: number
  tone: ToastTone
  message: string
  /**
   * Milisegundos que vive el aviso. 0 lo deja pegado hasta que se descarte a
   * mano, para lo que no puede desaparecer sin que el usuario se entere.
   */
  duration: number
}

const DEFAULT_DURATION = 5000
/** Los errores duran más: un fallo necesita más tiempo para ser leído que un ok. */
const ERROR_DURATION = 8000

/**
 * Tope de avisos a la vez. Sin él, un bucle que falla en cada iteración apila
 * veinte avisos y tapa la pantalla entera, que es justo lo contrario de lo que
 * un aviso debe hacer.
 */
const MAX_VISIBLE = 3

const toasts = ref<Toast[]>([])

/** Temporizadores por id, para poder cancelarlos al descartar el aviso. */
const timers = new Map<number, ReturnType<typeof setTimeout>>()

let nextId = 0

function dismiss(id: number) {
  const timer = timers.get(id)
  // Cancelar el temporizador es lo que evita que un aviso ya descartado a mano
  // vuelva a aparecer. Sin esto, cerrar un aviso a destiempo lo deja reaparecer
  // segundos después, como si se hubiera auto-deshecho.
  if (timer) {
    clearTimeout(timer)
    timers.delete(id)
  }
  toasts.value = toasts.value.filter((toast) => toast.id !== id)
}

function push(tone: ToastTone, message: string, duration?: number) {
  const id = nextId++
  const ms = duration ?? (tone === 'error' ? ERROR_DURATION : DEFAULT_DURATION)

  // Si se pasa el tope, se retira el más antiguo en vez de ignorar el nuevo. El
  // aviso nuevo suele importar más que el viejo, que ya ha pasado su rato.
  const overflowing = toasts.value.slice(0, Math.max(0, toasts.value.length - MAX_VISIBLE + 1))
  for (const old of overflowing) dismiss(old.id)

  toasts.value = [...toasts.value, { id, tone, message, duration: ms }]

  if (ms > 0) {
    timers.set(
      id,
      setTimeout(() => dismiss(id), ms),
    )
  }

  return id
}

function clear() {
  for (const timer of timers.values()) clearTimeout(timer)
  timers.clear()
  toasts.value = []
}

export function useToast() {
  return {
    toasts: readonly(toasts),
    success: (message: string, duration?: number) => push('success', message, duration),
    error: (message: string, duration?: number) => push('error', message, duration),
    dismiss,
    clear,
  }
}
