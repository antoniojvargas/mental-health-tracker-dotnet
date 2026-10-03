<script setup lang="ts">
import { computed } from 'vue'

import { METRICS } from './metrics'

/**
 * Selector de métricas del gráfico de tendencias.
 *
 * Los chips se construyen desde `METRICS`, así que etiqueta y color salen de la
 * misma definición que pinta el gráfico y no pueden desincronizarse. El tope es
 * de tres series a la vez: más líneas hacen ilegible la gráfica.
 *
 * Al llegar al tope se deshabilitan solo los chips NO seleccionados; los que ya
 * están dentro siguen activos para poder quitarlos. `aria-pressed` refleja el
 * estado, de modo que un lector de pantalla lo anuncia como botón de
 * alternancia.
 */

/** Series que admite la gráfica a la vez. */
const MAX_METRICS = 3

const props = withDefaults(
  defineProps<{
    /** Claves de las métricas seleccionadas. */
    modelValue?: string[]
  }>(),
  { modelValue: () => [] },
)

const emit = defineEmits<{
  (e: 'update:modelValue', value: string[]): void
}>()

const selected = computed(() => new Set(props.modelValue))

function toggle(key: string): void {
  if (selected.value.has(key)) {
    emit(
      'update:modelValue',
      props.modelValue.filter((metricKey) => metricKey !== key),
    )
    return
  }
  if (props.modelValue.length >= MAX_METRICS) return
  emit('update:modelValue', [...props.modelValue, key])
}
</script>

<template>
  <div class="flex flex-wrap gap-2" role="group" aria-label="Choose up to 3 metrics">
    <button
      v-for="metric in METRICS"
      :key="metric.key"
      type="button"
      :disabled="!selected.has(metric.key) && modelValue.length >= MAX_METRICS"
      :aria-pressed="selected.has(metric.key)"
      class="flex items-center gap-1.5 rounded-full border px-3 py-1.5 text-sm font-medium transition-colors duration-200 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-clearsky-500 disabled:cursor-not-allowed disabled:opacity-40"
      :class="
        selected.has(metric.key)
          ? 'border-transparent text-paper-50'
          : 'border-ink-100 bg-paper-50 text-ink-500 hover:bg-paper-100'
      "
      :style="selected.has(metric.key) ? { backgroundColor: metric.color } : undefined"
      @click="toggle(metric.key)"
    >
      <span
        class="h-2 w-2 rounded-full"
        :style="{ backgroundColor: selected.has(metric.key) ? 'white' : metric.color }"
        aria-hidden="true"
      />
      {{ metric.label }}
    </button>
  </div>
</template>
