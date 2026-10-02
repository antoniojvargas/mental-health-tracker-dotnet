<script setup lang="ts">
import { computed, ref, watch } from 'vue'

/**
 * Escala de estado de ánimo de 5 valores, implementada como radiogroup accesible.
 *
 * Usa `role="radiogroup"` + `role="radio"` con roving tabindex para que la
 * navegación por teclado con flechas (izquierda/derecha/arriba/abajo), Home/End,
 * Espacio/Enter funcione tal como espera un radiogroup WAI-ARIA.
 */

const props = withDefaults(
  defineProps<{
    /** Valor seleccionado (1-5). `null` significa sin selección. */
    modelValue?: number | null
    /** Deshabilita toda la interacción */
    disabled?: boolean
    /** Etiqueta accesible para el radiogroup */
    ariaLabel?: string
  }>(),
  {
    modelValue: null,
    disabled: false,
    ariaLabel: 'Mood scale',
  },
)

const emit = defineEmits<{
  (e: 'update:modelValue', value: number | null): void
}>()

const options = [
  { value: 1, label: 'Very low' },
  { value: 2, label: 'Low' },
  { value: 3, label: 'Neutral' },
  { value: 4, label: 'Good' },
  { value: 5, label: 'Very good' },
] as const

const activeIndex = computed(() => {
  if (props.modelValue == null) return -1
  const idx = options.findIndex((o) => o.value === props.modelValue)
  return idx >= 0 ? idx : -1
})

const focusedIndex = ref(activeIndex.value >= 0 ? activeIndex.value : 0)

watch(
  () => props.modelValue,
  (v) => {
    if (v == null) return
    const idx = options.findIndex((o) => o.value === v)
    if (idx >= 0) focusedIndex.value = idx
  },
)

function select(index: number) {
  if (props.disabled) return
  focusedIndex.value = index
  emit('update:modelValue', options[index].value)
}

function onKeydown(e: KeyboardEvent) {
  if (props.disabled) return
  const count = options.length
  switch (e.key) {
    case 'ArrowRight':
    case 'ArrowDown':
      e.preventDefault()
      focusedIndex.value = (focusedIndex.value + 1) % count
      break
    case 'ArrowLeft':
    case 'ArrowUp':
      e.preventDefault()
      focusedIndex.value = (focusedIndex.value - 1 + count) % count
      break
    case 'Home':
      e.preventDefault()
      focusedIndex.value = 0
      break
    case 'End':
      e.preventDefault()
      focusedIndex.value = count - 1
      break
    case ' ':
    case 'Enter':
      e.preventDefault()
      select(focusedIndex.value)
      break
    default:
      break
  }
}
</script>

<template>
  <div
    role="radiogroup"
    :aria-label="ariaLabel"
    :aria-disabled="disabled"
    class="flex flex-col gap-3"
    @keydown="onKeydown"
  >
    <div class="flex items-center justify-between gap-2 sm:gap-3">
      <div
        v-for="(opt, i) in options"
        :key="opt.value"
        role="radio"
        :aria-checked="modelValue === opt.value"
        :aria-label="opt.label"
        :tabindex="focusedIndex === i ? 0 : -1"
        :class="[
          'group relative flex h-10 w-10 cursor-pointer items-center justify-center rounded-full border text-sm font-medium transition-colors sm:h-11 sm:w-11',
          'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-clearsky-500',
          {
            'border-clearsky-600 bg-clearsky-600 text-paper-50 shadow-sm':
              modelValue === opt.value && !disabled,
            'border-ink-200 bg-paper-50 text-ink-700 hover:border-clearsky-300 hover:bg-clearsky-50':
              modelValue !== opt.value && !disabled,
            'cursor-not-allowed border-ink-200 bg-ink-50 text-ink-300 opacity-60': disabled,
          },
        ]"
        @click="select(i)"
        @focus="focusedIndex = i"
      >
        {{ opt.value }}
      </div>
    </div>

    <div class="flex items-center justify-between px-1 text-xs text-ink-500 sm:text-sm">
      <span
        v-for="opt in options"
        :key="`label-${opt.value}`"
        class="w-12 text-center sm:w-14"
        aria-hidden="true"
      >
        {{ opt.label }}
      </span>
    </div>

    <!-- Anuncio en vivo para lectores de pantalla de la etiqueta activa -->
    <span class="sr-only" aria-live="polite">
      {{ options[focusedIndex]?.label }}
    </span>
  </div>
</template>
