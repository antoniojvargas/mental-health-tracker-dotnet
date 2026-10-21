<script setup lang="ts">
import SliderField from '../SliderField.vue'
import { ACTIVITY_TYPE_OPTIONS, SOCIAL_FREQUENCY_OPTIONS } from '../daily-log-options'
import type { ActivityType, SocialFrequency } from '../../../types/daily-log'

/** Paso 3 del registro diario: actividad física y contacto social. */

const activityType = defineModel<ActivityType>('activityType', { required: true })
const activityMinutes = defineModel<number>('activityMinutes', { required: true })
const socialFrequency = defineModel<SocialFrequency>('socialFrequency', { required: true })
</script>

<template>
  <section class="flex flex-col gap-6">
    <h3 class="text-base font-medium text-ink-700">Activity and social life</h3>

    <div class="flex flex-col gap-2">
      <label for="activityType" class="text-sm font-medium text-ink-700">Activity type</label>
      <select
        id="activityType"
        v-model="activityType"
        class="min-h-11 rounded-lg border border-ink-200 bg-paper-50 px-3 py-2 text-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-clearsky-500"
      >
        <option v-for="opt in ACTIVITY_TYPE_OPTIONS" :key="opt.key" :value="opt.key">
          {{ opt.label }}
        </option>
      </select>
    </div>

    <SliderField
      v-model="activityMinutes"
      label="Activity minutes"
      :min="0"
      :max="600"
      :step="5"
      :disabled="activityType === 'none'"
      helper-text="0–600 minutes (disabled if no activity)"
    />

    <div class="flex flex-col gap-2">
      <label for="socialFrequency" class="text-sm font-medium text-ink-700"
        >Social contact frequency</label
      >
      <select
        id="socialFrequency"
        v-model="socialFrequency"
        class="min-h-11 rounded-lg border border-ink-200 bg-paper-50 px-3 py-2 text-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-clearsky-500"
      >
        <option v-for="opt in SOCIAL_FREQUENCY_OPTIONS" :key="opt.key" :value="opt.key">
          {{ opt.label }}
        </option>
      </select>
    </div>
  </section>
</template>
