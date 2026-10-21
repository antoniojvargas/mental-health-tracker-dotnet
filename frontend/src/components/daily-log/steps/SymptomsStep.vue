<script setup lang="ts">
import SymptomPicker from '../SymptomPicker.vue'
import type { SymptomEntry } from '../../../types/daily-log-form'

/** Paso 4 del registro diario: síntomas con su severidad y notas libres. */

const symptoms = defineModel<SymptomEntry[]>('symptoms', { required: true })
const notes = defineModel<string>('notes', { required: true })
</script>

<template>
  <section class="flex flex-col gap-4">
    <h3 class="text-base font-medium text-ink-700">Symptoms</h3>
    <SymptomPicker v-model="symptoms" aria-label="Symptoms" />
    <div class="flex flex-col gap-2">
      <label for="notes" class="text-sm font-medium text-ink-700">Notes</label>
      <textarea
        id="notes"
        v-model="notes"
        maxlength="1000"
        rows="4"
        placeholder="Additional notes..."
        class="rounded-lg border border-ink-200 bg-paper-50 px-3 py-2 text-sm placeholder:text-ink-400 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-clearsky-500"
      />
      <div class="flex justify-end text-xs text-ink-500">{{ notes.length }}/1000</div>
    </div>
  </section>
</template>
