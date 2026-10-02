<script setup lang="ts">
import { computed } from 'vue'

/**
 * Campo deslizante para valores numéricos con valor actual anunciado a lectores
 * de pantalla y etiqueta visible. El `aria-valuetext` opcional permite describir
 * el valor (por ejemplo "8 — Moderate") sin perder el número.
 */

const props = withDefaults(
  defineProps<{
    /** Etiqueta visible y accesible */
    label: string
    /** Valor actual */
    modelValue: number
    /** Mínimo del rango */
    min?: number
    /** Máximo del rango */
    max?: number
    /** Paso */
    step?: number
    /** Texto alternativo para el valor (anunciado por lector de pantalla) */
    valueText?: string | null
    /** Descripción textual debajo del slider */
    helperText?: string | null
    /** ID para enlazar label <-> input */
    id?: string | null
    /** Deshabilitado */
    disabled?: boolean
    /** Nombre del campo */
    name?: string | null
  }>(),
  {
    min: 0,
    max: 10,
    step: 1,
    valueText: null,
    helperText: null,
    id: null,
    disabled: false,
    name: null,
  },
)

const emit = defineEmits<{
  (e: 'update:modelValue', value: number): void
}>()

const inputId = computed(() => props.id ?? `slider-${crypto.randomUUID?.() ?? Math.random().toString(36).slice(2)}`)

function onInput(e: Event) {
  const target = e.target as HTMLInputElement | null
  if (!target) return
  emit('update:modelValue', Number(target.value))
}
</script>

<template>
  <div class="flex flex-col gap-2">
    <div class="flex items-center justify-between">
      <label :for="inputId" class="text-sm font-medium text-ink-900">
        {{ label }}
      </label>
      <output
        :for="inputId"
        :aria-live="valueText ? 'polite' : 'off'"
        :aria-valuetext="valueText ?? undefined"
        class="text-sm tabular-nums text-ink-700"
      >
        {{ valueText ? valueText : modelValue }}
      </output>
    </div>

    <input
      :id="inputId"
      type="range"
      :name="name || undefined"
      :min="min"
      :max="max"
      :step="step"
      :value="modelValue"
      :disabled="disabled"
      :aria-valuetext="valueText ?? undefined"
      class="h-2 w-full cursor-pointer appearance-none rounded-full bg-ink-200 accent-clearsky-600 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-clearsky-500 disabled:cursor-not-allowed disabled:opacity-60"
      @input="onInput"
    />

    <p v-if="helperText" class="text-xs text-ink-500">{{ helperText }}</p>
  </div>
</template>
