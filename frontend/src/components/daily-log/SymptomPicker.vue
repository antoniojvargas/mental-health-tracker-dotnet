<script setup lang="ts">
import { computed } from 'vue'

/**
 * Selector de síntomas para registro diario.
 * 8 síntomas del dominio (anxiety, depression, sleep, appetite, energy,
 * concentration, irritability, pain). Cada síntoma puede activarse/desactivarse
 * y, al activarse, incluye un control de severidad 1-5.
 */

export interface SymptomEntry {
  /** Clave estable para el síntoma */
  key: string
  /** Etiqueta legible */
  label: string
  /** Activo */
  active: boolean
  /** Severidad 1-5 cuando está activo */
  severity: number
}

export type SymptomKey =
  | 'anxiety'
  | 'depression'
  | 'sleep'
  | 'appetite'
  | 'energy'
  | 'concentration'
  | 'irritability'
  | 'pain'

const props = withDefaults(
  defineProps<{
    /** Lista de síntomas; si no se pasa, usa defaults del dominio */
    modelValue?: SymptomEntry[]
    /** Deshabilita todo el control */
    disabled?: boolean
    /** Aria label para el grupo */
    ariaLabel?: string
  }>(),
  {
    modelValue: () => [
      { key: 'anxiety', label: 'Anxiety', active: false, severity: 1 },
      { key: 'depression', label: 'Depression', active: false, severity: 1 },
      { key: 'sleep', label: 'Sleep', active: false, severity: 1 },
      { key: 'appetite', label: 'Appetite', active: false, severity: 1 },
      { key: 'energy', label: 'Energy', active: false, severity: 1 },
      { key: 'concentration', label: 'Concentration', active: false, severity: 1 },
      { key: 'irritability', label: 'Irritability', active: false, severity: 1 },
      { key: 'pain', label: 'Pain', active: false, severity: 1 },
    ],
    disabled: false,
    ariaLabel: 'Symptom picker',
  },
)

const emit = defineEmits<{
  (e: 'update:modelValue', value: SymptomEntry[]): void
}>()

const entries = computed(() => props.modelValue)

function toggle(key: string) {
  if (props.disabled) return
  const next = entries.value.map((s) => {
    if (s.key !== key) return s
    const active = !s.active
    // Al desactivar, mantenemos severidad por coherencia (no requiere cambio),
    // pero si se activa y estaba en 0, normalizamos a 1.
    const severity = active ? Math.max(1, s.severity || 1) : s.severity
    return { ...s, active, severity }
  })
  emit('update:modelValue', next)
}

function setSeverity(key: string, severity: number) {
  if (props.disabled) return
  const clamped = Math.min(5, Math.max(1, severity))
  const next = entries.value.map((s) => (s.key === key ? { ...s, severity: clamped, active: true } : s))
  emit('update:modelValue', next)
}
</script>

<template>
  <fieldset
    class="flex flex-col gap-4 rounded-2xl border border-ink-200 bg-paper-50 p-4"
    :aria-label="ariaLabel"
    :disabled="disabled"
  >
    <legend class="px-1 text-sm font-medium text-ink-900">{{ ariaLabel }}</legend>

    <div class="flex flex-col gap-3">
      <div v-for="sym in entries" :key="sym.key" class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <label class="flex cursor-pointer items-center gap-3" :for="`sym-check-${sym.key}`">
          <input
            :id="`sym-check-${sym.key}`"
            type="checkbox"
            :checked="sym.active"
            :disabled="disabled"
            class="h-4 w-4 cursor-pointer accent-clearsky-600 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-clearsky-500 disabled:cursor-not-allowed disabled:opacity-60"
            @change="toggle(sym.key)"
          />
          <span class="text-sm text-ink-900">{{ sym.label }}</span>
        </label>

        <div v-if="sym.active" class="flex items-center gap-3 pl-7 sm:pl-0">
          <span class="text-xs text-ink-500">Severity</span>
          <div
            class="flex items-center gap-1"
            role="radiogroup"
            :aria-label="`${sym.label} severity`"
          >
            <button
              v-for="n in 5"
              :key="n"
              type="button"
              role="radio"
              :aria-checked="sym.severity === n"
              :tabindex="sym.severity === n ? 0 : -1"
              :disabled="disabled"
              class="flex h-8 w-8 items-center justify-center rounded-full border text-xs font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-clearsky-500 disabled:cursor-not-allowed disabled:opacity-60"
              :class="[
                sym.severity === n
                  ? 'border-clearsky-600 bg-clearsky-600 text-paper-50'
                  : 'border-ink-200 bg-paper-50 text-ink-700 hover:border-clearsky-300 hover:bg-clearsky-50',
              ]"
              @click="setSeverity(sym.key, n)"
            >
              {{ n }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </fieldset>
</template>
