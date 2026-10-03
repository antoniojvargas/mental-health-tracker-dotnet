<script setup lang="ts">
import type { LogRange } from '../../stores/useLogsStore'

/**
 * Conmutador de ventana temporal del gráfico de tendencias.
 *
 * Un control segmentado de dos opciones que no son excluyentes de facto pero sí
 * a efectos de la vista: solo una ventana está activa. Por eso es un grupo de
 * botones con `aria-pressed` en cada uno y no un `radiogroup`: el estado es
 * "pulsado o no" por opción, y el lector de pantalla lo anuncia como tal.
 */

const RANGES: { value: LogRange; label: string }[] = [
  { value: 'week', label: 'Week' },
  { value: 'month', label: 'Month' },
]

defineProps<{
  /** Ventana activa. */
  modelValue: LogRange
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: LogRange): void
}>()
</script>

<template>
  <div class="inline-flex rounded-full bg-paper-200 p-1" role="group" aria-label="Time range">
    <button
      v-for="range in RANGES"
      :key="range.value"
      type="button"
      :aria-pressed="modelValue === range.value"
      class="rounded-full px-4 py-1.5 text-sm font-medium transition-colors duration-200 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-clearsky-500"
      :class="
        modelValue === range.value
          ? 'bg-paper-50 text-ink-700 shadow-sm'
          : 'text-ink-400 hover:text-ink-600'
      "
      @click="emit('update:modelValue', range.value)"
    >
      {{ range.label }}
    </button>
  </div>
</template>
