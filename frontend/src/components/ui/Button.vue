<script setup lang="ts">
import { computed } from 'vue'

/**
 * Botón base del UI kit. Tres variantes, tres estados de carga.
 *
 * Renderiza un `<button>` real, no un `<div>` con un manejador de clic: el
 * Enter del teclado, el Espacio, el foco y el estado disabled vienen gratis del
 * elemento nativo, y reconstruirlos a mano es donde aparecen los bugs.
 */

type Variant = 'primary' | 'ghost' | 'subtle'

const props = withDefaults(
  defineProps<{
    /**
     * `primary` para la acción principal de una pantalla —una por vista—. `subtle`
     * para acciones secundarias con peso. `ghost` para lo terciario: solo texto,
     * sin caja, para lo que puede vivir en un pie o junto a otra acción.
     */
    variant?: Variant
    type?: 'button' | 'submit' | 'reset'
    disabled?: boolean
    loading?: boolean
  }>(),
  {
    variant: 'primary',
    // `type="button"` por defecto a propósito: dentro de un `<form>` un `<button>`
    // sin tipo se envía al pulsar Enter en otro campo, y un botón que solo navega
    // no debería mandar el formulario. Quien quiera enviar, dice `submit`.
    type: 'button',
    disabled: false,
    loading: false,
  },
)

/**
 * Cargar deshabilita, pero no es lo mismo que estar deshabilitado: mientras carga
 * el botón sigue siendo el mismo objeto y su etiqueta sigue siendo la misma, así
 * que se combina el estado con `||` en vez de sustituir una variable por otra.
 */
const isInert = computed(() => props.disabled || props.loading)
</script>

<template>
  <button
    :type="type"
    :disabled="isInert"
    :aria-busy="loading"
    :class="[
      'font-sans inline-flex items-center justify-center gap-2 rounded-xl px-5 py-3 text-sm font-medium transition-colors',
      // El anillo de foco va siempre en la misma nota: la variante cambia el
      // relleno, nunca la señal de foco, para que el teclado lea lo mismo en los
      // tres botones. `outline` y no `ring` porque el anillo se pinta fuera del
      // borde y no se lo come un `overflow` del contenedor.
      'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-clearsky-500',
      {
        // Un botón que carga o que está deshabilitado baja a `ink-300` sobre
        // `ink-50`: se ve claramente apagado sin desaparecer, para que el texto
        // siga siendo legible y se entienda que ahí hay algo, no que no hay nada.
        'border border-ink-200 bg-paper-50 text-ink-600 hover:border-clearsky-300 hover:bg-clearsky-50 hover:text-ink-700':
          variant === 'subtle',
        'border border-transparent bg-clearsky-600 text-paper-50 hover:bg-clearsky-700':
          variant === 'primary',
        'border border-transparent bg-transparent text-ink-500 hover:bg-ink-100 hover:text-ink-700':
          variant === 'ghost',
        'cursor-not-allowed opacity-60': isInert,
      },
    ]"
  >
    <!--
      El spinner va delante del texto y no lo sustituye: quitar la etiqueta haría
      que el ancho del botón saltara al terminar la carga, y el texto es lo que
      dice qué está pasando. `animate-spin` es la única animación continua del
      kit, y va aquí porque el resto de la paleta las reserva para lo que se mira
      a menudo, no para lo que espera un clic.
    -->
    <svg
      v-if="loading"
      class="h-4 w-4 animate-spin"
      viewBox="0 0 24 24"
      fill="none"
      aria-hidden="true"
    >
      <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" />
      <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 0 1 8-8v4a4 4 0 0 0-4 4H4z" />
    </svg>
    <slot />
  </button>
</template>
