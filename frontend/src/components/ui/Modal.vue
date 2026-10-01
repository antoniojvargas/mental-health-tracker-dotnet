<script lang="ts">
/**
 * Pila de modales abiertos, de fuera hacia dentro, y el overflow que había antes
 * del primero.
 *
 * Este bloque es `<script>` normal y no `<script setup>` a propósito: lo que se
 * declara en `<script setup>` se ejecuta una vez por instancia, así que una
 * `openStack` de ahí le daría a cada modal su propia pila privada, y cada uno se
 * vería a sí mismo como el único abierto. Solo sirve si la comparten, y lo
 * único de verdad común entre dos instancias es el módulo.
 */
export const openStack: HTMLElement[] = []

/** Entablón de scroll anterior al primer modal, para restaurarlo tal cual. */
let previousOverflow = ''
</script>

<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, ref, useId, watch } from 'vue'

/**
 * Diálogo modal accesible. Cierra con Escape, atrapa el foco dentro de sí mismo y
 * lo devuelve al elemento que lo abrió al cerrarse.
 *
 * No usa ninguna librería de focus trap: son unas 30 líneas y el comportamiento
 * cabe en un `<dialog>` nativo, así que meter una dependencia por esto sería
 * pagar mantenimiento por algo que ya está resuelto aquí.
 *
 * La trampa del foco se sostiene en dos cosas, y las dos hacen falta. El `Tab`
 * se intercepta para dar la vuelta al final de la lista de enfocables, que es lo
 * que el usuario nota; y un `focusin` en el documento devuelve el foco si se
 * escapa por otra vía, que es lo que el usuario no nota pero deja el modal
 * abierto mientras el foco está detrás.
 */

const props = withDefaults(
  defineProps<{
    open: boolean
    /**
     * Texto del `aria-labelledby`. Es una prop y no un slot porque el nombre
     * accesible del diálogo no puede depender de que quien lo use se acuerde de
     * ponerlo: un modal sin nombre no lo anuncia un lector de pantalla, y eso
     * solo se ve cuando ya está en producción.
     */
    title: string
    /** Cierra al pulsar fuera del panel. */
    closeOnBackdrop?: boolean
  }>(),
  { closeOnBackdrop: true },
)

const emit = defineEmits<{ close: [] }>()

// `useId` da un id único por instancia, así que dos modales abiertos a la vez no
// se pisan el `aria-labelledby`.
const titleId = `${useId()}-title`

const panel = ref<HTMLElement | null>(null)

/** Foco previo a la apertura: el elemento al que hay que devolverlo al cerrar. */
let previouslyFocused: HTMLElement | null = null

// `openStack` y `previousOverflow` viven en el bloque `<script>` de arriba porque
// tienen que ser de todos: el scroll del `body` solo se restaura al cerrar el
// último modal, y el foco vuelve al elemento que tenía cada uno.

/**
 * Elementos en los que el foco puede entrar. Se filtran por atributo y no por
 * `offsetParent`: en un entorno de test real no hay layout, así que `offsetParent`
 * siempre es nulo y el filtro vaciaría la lista entera.
 */
const FOCUSABLE = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled]):not([type="hidden"])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(',')

const focusable = () =>
  Array.from(panel.value?.querySelectorAll<HTMLElement>(FOCUSABLE) ?? []).filter(
    (el) => !el.hidden && el.getAttribute('aria-hidden') !== 'true',
  )

/** Diapositiva mientras el foco se devuelve al cerrar, para no pelear con el trap. */
let returning = false

/** ¿Es este modal el último de la pila, es decir, el que está encima? */
function encima() {
  return openStack[openStack.length - 1] === panel.value
}

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    // Solo reacciona el modal de encima. Con dos abiertos hay dos listeners de
    // `keydown` en el mismo `document` y los dos ven el tecleo, así que aquí
    // `stopPropagation` no sirve de nada: frena la subida por el árbol, no a otro
    // listener del mismo nodo. Sin esta comprobación un Escape cerraba los dos,
    // y con ellos el formulario que hubiera detrás.
    if (!encima()) return

    event.preventDefault()
    emit('close')
    return
  }

  if (event.key !== 'Tab') return

  // Aquí no hace falta la comprobación de `encima()` que sí está en el Escape:
  // aunque el modal de debajo robe el foco con este `Tab`, su propio `focusin`
  // se lo devuelve al de encima en el mismo tecleo. Comprobado con un mutante:
  // quitar la guarda deja los 28 tests en verde.
  const targets = focusable()
  // Sin nada enfocable dentro no hay a dónde ir con el Tab, y el panel con
  // `tabindex="-1"` es el único sitio donde puede quedarse el foco.
  if (targets.length === 0) {
    event.preventDefault()
    panel.value?.focus()
    return
  }

  const first = targets[0]
  const last = targets[targets.length - 1]
  const active = document.activeElement

  // Solo se interceptan los dos extremos. En medio el navegador ya hace lo
  // correcto, y la tontería mayor sería pelear con el navegador en cada tecleo.
  if (event.shiftKey && (active === first || active === panel.value)) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && active === last) {
    event.preventDefault()
    first.focus()
  }
}

/**
 * Devuelve el foco si ha salido del diálogo. Cubre lo que el `Tab` no ve: un
 * `autofocus` de un hijo, un clic en otro sitio de la página, o el foco que se
 * lleva el propio navegador al perder la ventana.
 */
function onFocusin(event: FocusEvent) {
  if (returning || !props.open || !panel.value) return

  // Solo el de encima vigila el foco. Todos se teletransportan al `body`, así que
  // ninguno cuelga del panel de los demás: si los dos vigilaran, el de abajo le
  // quitaría el foco al de arriba y el de arriba se lo devolvería, en bucle,
  // hasta reventar la pila de llamadas.
  if (!encima()) return

  const target = event.target as Node | null
  if (target && !panel.value.contains(target)) {
    ;(focusable()[0] ?? panel.value).focus()
  }
}

function lockScroll() {
  if (openStack.length > 0) return
  previousOverflow = document.body.style.overflow
  document.body.style.overflow = 'hidden'
}

function unlockScroll() {
  if (openStack.length > 0) return
  document.body.style.overflow = previousOverflow
}

function onBackdropClick() {
  if (props.closeOnBackdrop) emit('close')
}

watch(
  () => props.open,
  async (isOpen, wasOpen) => {
    if (isOpen) {
      // Antes de bloquear nada: `previouslyFocused` tiene que ser lo que estaba
      // activo, no el propio modal.
      previouslyFocused = document.activeElement as HTMLElement | null
      lockScroll()

      // Espera al render para que el panel ya exista en el DOM. Sin esto,
      // `focusable()` no encuentra nada y el foco se queda donde estaba, detrás del
      // overlay, que es justo el fallo que hace inaccesible a un modal.
      await nextTick()
      // La pila se rellena aquí y no antes: el watcher corre antes del render, así
      // que `panel.value` todavía era `null` y la pila guardaba nulos. Con eso
      // `encima()` no encontraba nunca su propio panel y el Escape no cerraba.
      openStack.push(panel.value as HTMLElement)
      ;(focusable()[0] ?? panel.value)?.focus()
    } else if (wasOpen) {
      // `indexOf` y no `pop`: si el Escape ya lo sacó de la pila, este `pop`
      // se llevaría la entrada del modal de debajo y dejaría al de encima sin
      // poder recibir un Escape.
      const mio = openStack.indexOf(panel.value as HTMLElement)
      if (mio !== -1) openStack.splice(mio, 1)
      unlockScroll()
      returning = true
      previouslyFocused?.focus()
      // El flag se levanta después del foco para no capturar el `focusin` que
      // genera esa misma llamada. Sin espera, el trap leería el retorno como un
      // escape y devolvería el foco al panel que se está cerrando.
      await nextTick()
      returning = false
      previouslyFocused = null
    }
  },
)

onMounted(() => {
  document.addEventListener('keydown', onKeydown)
  document.addEventListener('focusin', onFocusin)
})

onBeforeUnmount(() => {
  document.removeEventListener('keydown', onKeydown)
  document.removeEventListener('focusin', onFocusin)
  // Cerrar el componente sin haberlo cerrado deja el `body` bloqueado para
  // siempre, que es un fallo invisible y muy molesto de depurar.
  if (props.open) {
    const mio = openStack.indexOf(panel.value as HTMLElement)
    if (mio !== -1) openStack.splice(mio, 1)
    unlockScroll()
  }
})
</script>

<template>
  <Teleport to="body">
    <Transition
      enter-active-class="transition-opacity duration-200 ease-out"
      enter-from-class="opacity-0"
      leave-active-class="transition-opacity duration-200 ease-out"
      leave-to-class="opacity-0"
    >
      <!--
        Solo aparece con `open`. El overlay es el elemento que recibe el clic de
        fuera, y lleva `inset-0` para cubrir la ventana entera.
      -->
      <div
        v-if="open"
        class="fixed inset-0 z-50 flex items-center justify-center bg-ink-700/40 p-6"
        @click="onBackdropClick"
      >
        <!--
          `tabindex="-1"` para que el panel pueda recibir el foco por script. Sin
          él, un diálogo sin nada enfocable dentro sería inalcanzable con el
          teclado y el foco se quedaría detrás del overlay.

          `role="dialog"` con `aria-modal="true"`: el segundo le dice al lector
          de pantalla que lo de fuera está apagado, y es lo que le permite no
          ir leyendo el contenido de la página que queda debajo.
        -->
        <div
          ref="panel"
          role="dialog"
          aria-modal="true"
          :aria-labelledby="titleId"
          tabindex="-1"
          class="animate-slide-up w-full max-w-md rounded-2xl border border-ink-100 bg-paper-50 px-8 py-6 shadow-lg"
          @click.stop
        >
          <h2 :id="titleId" class="font-display text-lg font-semibold text-ink-700">
            {{ title }}
          </h2>

          <div class="font-sans mt-3 text-sm leading-relaxed text-ink-500">
            <slot />
          </div>

          <div v-if="$slots.actions" class="mt-6 flex justify-end gap-3">
            <slot name="actions" />
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>
