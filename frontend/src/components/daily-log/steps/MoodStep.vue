<script setup lang="ts">
import MoodScale from '../MoodScale.vue'
import SliderField from '../SliderField.vue'

/** Paso 1 del registro diario: ánimo, ansiedad y estrés. */

const mood = defineModel<number | null>('mood', { required: true })
const anxiety = defineModel<number>('anxiety', { required: true })
const stress = defineModel<number>('stress', { required: true })
</script>

<template>
  <section class="flex flex-col gap-6">
    <h3 class="text-base font-medium text-ink-700">Mood</h3>
    <MoodScale v-model="mood" aria-label="Mood" aria-required="true" />
    <SliderField
      v-model="anxiety"
      label="Anxiety"
      :min="1"
      :max="10"
      :step="1"
      helper-text="1 = very low, 10 = very high"
    />
    <SliderField
      v-model="stress"
      label="Stress"
      :min="1"
      :max="10"
      :step="1"
      helper-text="1 = very low, 10 = very high"
    />
  </section>
</template>
