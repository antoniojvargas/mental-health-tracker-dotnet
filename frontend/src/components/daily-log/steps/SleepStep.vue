<script setup lang="ts">
import SliderField from '../SliderField.vue'
import { SLEEP_DISTURBANCE_OPTIONS } from '../daily-log-options'
import type { SleepDisturbance } from '../../../types/daily-log'

/** Paso 2 del registro diario: horas y calidad de sueño, y alteraciones. */

const sleepHours = defineModel<number>('sleepHours', { required: true })
const sleepQuality = defineModel<number>('sleepQuality', { required: true })
const sleepDisturbances = defineModel<SleepDisturbance[]>('sleepDisturbances', { required: true })
</script>

<template>
  <section class="flex flex-col gap-6">
    <h3 class="text-base font-medium text-ink-700">Sleep</h3>
    <SliderField
      v-model="sleepHours"
      label="Sleep hours"
      :min="0"
      :max="24"
      :step="0.5"
      helper-text="Approximate hours of sleep (0–24)"
    />
    <SliderField
      v-model="sleepQuality"
      label="Sleep quality"
      :min="1"
      :max="5"
      :step="1"
      helper-text="1 = very poor, 5 = very good"
    />
    <fieldset class="flex flex-col gap-3 rounded-2xl border border-ink-200 bg-paper-50 p-4">
      <legend class="px-1 text-sm font-medium text-ink-700">
        Sleep disturbances (select all that apply)
      </legend>
      <div class="grid gap-2 sm:grid-cols-2">
        <label
          v-for="opt in SLEEP_DISTURBANCE_OPTIONS"
          :key="opt.key"
          class="flex min-h-11 cursor-pointer items-center gap-2 rounded-lg border border-transparent px-2 py-2 hover:bg-ink-50"
        >
          <input
            v-model="sleepDisturbances"
            type="checkbox"
            :value="opt.key"
            class="h-5 w-5 accent-clearsky-600 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-clearsky-500"
          />
          <span class="text-sm text-ink-700">{{ opt.label }}</span>
        </label>
      </div>
    </fieldset>
  </section>
</template>
