<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, useId } from 'vue'

/**
 * Tooltip accesible. Aparece al pasar el ratón o al enfocar, y se va al salir.
 *
 * Se muestra en `focus` tanto como en `hover` a propósito. Un tooltip que solo
 * sale con el ratón es un tooltip que no existe para quien navega con el
 * teclado, que es la forma más común de moverse por un formulario.
 *
 * No usa Popper ni nada parecido: el posicionamiento es un `position: fixed` con
 * cuatro estilos calculados, y meter una librería de colisión por un texto corto
 * sería pagar mantenimiento por un problema que aquí no existe.
 */

const props = withDefaults(
  defineProps<{
    /** Texto del tooltip. Es también el nombre accesible del disparador. */
    text: string
    /** Lado preferido. Si no cabe, se voltea al contrario. */
    placement?: 'top' | 'bottom' | 'left' | 'right'
    /** Retardo al abrir con el ratón, para no molestar al pasar el cursor. */
    delay?: number
  }>(),
  { placement: 'top', delay: 200 },
)

/**
 * `useId` da un id único por instancia, así que dos tooltips abiertos a la vez no
 * pueden acabar compartiendo el `aria-describedby` y describiéndose el uno al otro.
 * Es lo mismo que hace el Modal con su título, y por el mismo motivo.
 */
const id = `tooltip-${useId()}`

const trigger = ref<HTMLElement | null>(null)
const visible = ref(false)
const placement = ref(props.placement)

let openTimer: ReturnType<typeof setTimeout> | undefined

function show() {
  if (openTimer !== undefined) clearTimeout(openTimer)
  openTimer = setTimeout(() => {
    visible.value = true
    // Se mide después de mostrarlo: si no está en el DOM no hay caja que medir.
    placement.value = fits() ? props.placement : flip(props.placement)
  }, props.delay)
}

function hide() {
  if (openTimer !== undefined) {
    clearTimeout(openTimer)
    openTimer = undefined
  }
  visible.value = false
}

const OPPOSITE = { top: 'bottom', bottom: 'top', left: 'right', right: 'left' } as const
const flip = (side: (typeof OPPOSITE)[keyof typeof OPPOSITE]) => OPPOSITE[side]

/**
 * ¿Cabe del lado preferido? Se mide con el borde de la ventana y no con el del
 * contenedor: un tooltip dentro de un `overflow-hidden` se saldría por el borde
 * de la ventana sin salirse del padre, y es el de la ventana el que importa.
 */
function fits() {
  const box = trigger.value?.getBoundingClientRect()
  if (!box) return true
  const TIP = 8
  return (
    (props.placement === 'top' && box.top > TIP) ||
    (props.placement === 'bottom' && window.innerHeight - box.bottom > TIP) ||
    (props.placement === 'left' && box.left > TIP) ||
    (props.placement === 'right' && window.innerWidth - box.right > TIP)
  )
}

/**
 * El tooltip va en `fixed` y sus coordenadas se escriben en `style`, en vez de
 * posicionarse con clases. En un `fixed` los offsets son respecto a la ventana, y
 * calcularlos aquí es lo que permite que el tooltip escape del `overflow-hidden`
 * de un padre, que es donde se iría si fuera un hijo normal con `absolute`.
 */
const position = computed(() => {
  const box = trigger.value?.getBoundingClientRect()
  if (!box) return {}
  return {
    top: `${box.top}px`,
    left: `${box.left}px`,
  }
})

onMounted(() => {
  // `Escape` con el tooltip abierto lo cierra. Se pone en el documento y no en el
  // disparador para que funcione también con el foco dentro del tooltip.
  document.addEventListener('keydown', onKeydown)
})

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape' && visible.value) hide()
}

onBeforeUnmount(() => {
  document.removeEventListener('keydown', onKeydown)
  if (openTimer !== undefined) clearTimeout(openTimer)
})
</script>

<template>
  <span class="relative inline-flex" @mouseenter="show" @mouseleave="hide">
    <!--
      El comentario va dentro del `<span>`, no antes. En la raíz de un `<template>`
      convierte el componente en un fragmento de varios nodos y Vue deja de
      heredar atributos y listeners al elemento, que es justo lo que este
      envoltorio necesita: es el que recibe el `mouseenter` y el que se mide.

      `inline-flex` porque el disparador es un botón o un icono y hay que medir su
      caja para colocar el tooltip al lado. Un `div` de bloque se estiraría a todo
      el ancho y el tooltip se iría a la derecha del todo.
    -->
    <button
      ref="trigger"
      type="button"
      class="inline-flex rounded focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-clearsky-500"
      :aria-describedby="visible ? id : undefined"
      @focus="visible = true"
      @blur="hide"
    >
      <slot />
    </button>

    <Transition
      enter-active-class="transition-opacity duration-200 ease-out"
      enter-from-class="opacity-0"
      leave-active-class="transition-opacity duration-200 ease-out"
      leave-to-class="opacity-0"
    >
      <!--
        `role="tooltip"` y no `title`. El `title` nativo no se puede alcanzar con
        el teclado y tarda unos segundos en aparecer en algunos navegadores; aquí
        aparece al enfocar y se va al salir.

        El texto va suelto, sin `aria-label`: si el elemento lo nombra con un
        `aria-label`, el lector de pantalla anuncia la etiqueta en vez del texto
        visible, y aquí son la misma cosa.
      -->
      <span
        v-if="visible"
        :id="id"
        role="tooltip"
        class="font-sans pointer-events-none fixed z-50 max-w-56 rounded-lg bg-ink-700 px-3 py-1.5 text-xs text-paper-50 shadow-sm"
        :style="position"
      >
        {{ text }}
      </span>
    </Transition>
  </span>
</template>
