<script setup lang="ts">
import { useToast } from '../../composables/useToast'

/**
 * Contenedor de avisos. Se monta una vez, en `App.vue` o en el layout, y a partir
 * de ahí cualquier parte de la app puede lanzar un aviso sin montar nada.
 *
 * `role="status"` va en el contenedor y no en cada aviso, y es a propósito: es la
 * región viva que el lector de pantalla vigila. Un `role="status"` por aviso
 * ocupa un nodo nuevo cada vez, y las regiones vivas se anuncian al insertarse,
 * no al cambiar: el aviso se oye, pero como una interrupción en medio de otra
 * cosa, que es justo lo que `status` —polite— existe para evitar.
 */

const { toasts, dismiss } = useToast()
</script>

<template>
  <!--
    `aria-live="polite"` y `aria-atomic="false"`: un aviso nuevo se anuncia sin
    interrumpir lo que el lector de pantalla estuviera leyendo. Con
    `aria-atomic="true"` en un contenedor apilado, cada aviso nuevo volvería a
    leer los que ya estaban, y un error repetido se oiría tres veces.

    `pointer-events-none` en el contenedor con `pointer-events-auto` en cada
    aviso: así la pila no bloquea los clics de lo que hay debajo, pero los
    botones de cerrar sí funcionan.
  -->
  <div
    role="status"
    aria-live="polite"
    aria-atomic="false"
    class="pointer-events-none fixed inset-x-0 bottom-0 z-50 flex flex-col items-center gap-2 p-6"
  >
    <div
      v-for="toast in toasts"
      :key="toast.id"
      class="animate-slide-up pointer-events-auto flex w-full max-w-sm items-start gap-3 rounded-xl border px-4 py-3 shadow-sm"
      :class="
        toast.tone === 'success'
          ? 'border-meadow-200 bg-paper-50 text-ink-700'
          : 'border-ink-200 bg-ink-50 text-ink-700'
      "
    >
      <!--
        El tono se marca con `aria-hidden` en el icono y con texto en el aviso:
        un lector de pantalla no deduce un error de un triángulo, así que el
        icono es decoración y el significado va escrito.
      -->
      <span
        class="mt-0.5 h-2 w-2 shrink-0 rounded-full"
        :class="toast.tone === 'success' ? 'bg-meadow-500' : 'bg-ink-400'"
        aria-hidden="true"
      />

      <p class="font-sans flex-1 text-sm leading-relaxed">{{ toast.message }}</p>

      <!--
        Cierre a mano. Importa más que cualquier pausa automática: si el aviso se
        va solo en 5s, alguien con lector de pantalla puede no llegar a oírlo, y
        un fallo que desaparece sin dejar rastro no sirve de nada.
      -->
      <button
        type="button"
        class="font-sans -mt-0.5 -mr-1 shrink-0 rounded p-1 text-ink-400 transition-colors hover:text-ink-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-clearsky-500"
        :aria-label="`Descartar: ${toast.message}`"
        @click="dismiss(toast.id)"
      >
        <svg viewBox="0 0 20 20" class="h-4 w-4" fill="none" aria-hidden="true">
          <path
            d="M5 5l10 10M15 5L5 15"
            stroke="currentColor"
            stroke-width="1.5"
            stroke-linecap="round"
          />
        </svg>
      </button>
    </div>
  </div>
</template>
