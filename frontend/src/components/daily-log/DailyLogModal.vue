<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import Modal from '../ui/Modal.vue'
import Button from '../ui/Button.vue'
import MoodScale from './MoodScale.vue'
import SliderField from './SliderField.vue'
import SymptomPicker, { type SymptomEntry } from './SymptomPicker.vue'
import { create as createLog } from '../../services/logs.api'
import { useToast } from '../../composables/useToast'
import { ApiError } from '../../services/api-client'
import type { CreateDailyLogInput, ActivityType, SocialFrequency, SymptomType, SleepDisturbance } from '../../types/daily-log'

/**
 * Modal multipaso para registrar un Daily Log.
 * Pasos: 1) Mood  2) Sleep  3) Activity and social life  4) Symptoms
 * Incluye indicador de progreso y navegación atrás/adelante.
 */

const props = withDefaults(
  defineProps<{
    open: boolean
    title?: string
  }>(),
  {
    title: 'Daily log',
  },
)

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'submit', data: DailyLogForm): void
  (e: 'saved'): void
}>()

const steps = ['Mood', 'Sleep', 'Activity and social life', 'Symptoms'] as const
type StepIndex = 0 | 1 | 2 | 3
const currentStep = ref<StepIndex>(0)
const isSubmitting = ref(false)
const { success, error: toastError } = useToast()

const sleepDisturbanceOptions = [
  { key: 'trouble-falling-asleep', label: 'Trouble falling asleep' },
  { key: 'waking-during-night', label: 'Waking up during the night' },
  { key: 'waking-too-early', label: 'Waking up too early' },
  { key: 'nightmares', label: 'Nightmares' },
  { key: 'restless-legs', label: 'Restless legs' },
  { key: 'interrupted-sleep', label: 'Interrupted sleep' },
  { key: 'snoring', label: 'Snoring' },
  { key: 'other', label: 'Other' },
] as const

const activityTypeOptions = [
  { key: 'none', label: 'None' },
  { key: 'walk', label: 'Walk' },
  { key: 'run', label: 'Run' },
  { key: 'gym', label: 'Gym' },
  { key: 'yoga', label: 'Yoga/Stretching' },
  { key: 'cycling', label: 'Cycling' },
  { key: 'sports', label: 'Sports' },
  { key: 'housework', label: 'Housework' },
  { key: 'other', label: 'Other' },
] as const

const socialFrequencyOptions = [
  { key: 'none', label: 'None' },
  { key: 'once', label: 'Once' },
  { key: 'few', label: 'A few times' },
  { key: 'several', label: 'Several times' },
  { key: 'daily', label: 'Daily' },
] as const

const form = reactive<DailyLogForm>({
  mood: null,
  anxiety: 5,
  stress: 5,
  sleepQuality: 3,
  sleepHours: 7,
  sleepDisturbances: [],
  activityType: 'none',
  activityMinutes: 0,
  activity: 5,
  socialFrequency: 'none',
  social: 5,
  symptoms: [
    { key: 'anxiety', label: 'Anxiety', active: false, severity: 1 },
    { key: 'depression', label: 'Depression', active: false, severity: 1 },
    { key: 'sleep', label: 'Sleep', active: false, severity: 1 },
    { key: 'appetite', label: 'Appetite', active: false, severity: 1 },
    { key: 'energy', label: 'Energy', active: false, severity: 1 },
    { key: 'concentration', label: 'Concentration', active: false, severity: 1 },
    { key: 'irritability', label: 'Irritability', active: false, severity: 1 },
    { key: 'pain', label: 'Pain', active: false, severity: 1 },
  ],
  notes: '',
})

export interface DailyLogForm {
  mood: number | null
  anxiety: number
  stress: number
  sleepQuality: number
  sleepHours: number
  sleepDisturbances: string[]
  activityType: string
  activityMinutes: number
  activity: number
  socialFrequency: string
  social: number
  symptoms: SymptomEntry[]
  notes: string
}

const progress = computed(() => ((currentStep.value + 1) / steps.length) * 100)

const canGoNext = computed(() => {
  switch (currentStep.value) {
    case 0: // Mood
      return form.mood != null
    default:
      return true
  }
})

const isLastStep = computed(() => currentStep.value === steps.length - 1)

watch(
  () => props.open,
  (open) => {
    if (open) {
      currentStep.value = 0
    }
  },
)

function next() {
  if (!canGoNext.value) return
  if (currentStep.value < steps.length - 1) {
    currentStep.value = (currentStep.value + 1) as StepIndex
  }
}

function back() {
  if (currentStep.value > 0) {
    currentStep.value = (currentStep.value - 1) as StepIndex
  }
}

function submit() {
  if (!isLastStep.value) return
  emit('submit', { ...form, symptoms: form.symptoms.map((s) => ({ ...s })) })
}

function resetForm() {
  form.mood = null
  form.anxiety = 5
  form.stress = 5
  form.sleepQuality = 3
  form.sleepHours = 7
  form.sleepDisturbances = []
  form.activityType = 'none'
  form.activityMinutes = 0
  form.activity = 5
  form.socialFrequency = 'none'
  form.social = 5
  form.symptoms = [
    { key: 'anxiety', label: 'Anxiety', active: false, severity: 1 },
    { key: 'depression', label: 'Depression', active: false, severity: 1 },
    { key: 'sleep', label: 'Sleep', active: false, severity: 1 },
    { key: 'appetite', label: 'Appetite', active: false, severity: 1 },
    { key: 'energy', label: 'Energy', active: false, severity: 1 },
    { key: 'concentration', label: 'Concentration', active: false, severity: 1 },
    { key: 'irritability', label: 'Irritability', active: false, severity: 1 },
    { key: 'pain', label: 'Pain', active: false, severity: 1 },
  ]
  form.notes = ''
  currentStep.value = 0
}

function mapSleepDisturbances(dist: string[]): SleepDisturbance[] {
  const allowed = new Set(['none', 'insomnia', 'nightmares', 'frequent_waking', 'early_waking'])
  return dist
    .map((d) => {
      if (d === 'trouble-falling-asleep') return 'insomnia' as SleepDisturbance
      if (d === 'waking-during-night' || d === 'waking-too-early' || d === 'interrupted-sleep') return 'frequent_waking' as SleepDisturbance
      if (d === 'nightmares') return 'nightmares' as SleepDisturbance
      if (d === 'restless-legs') return 'frequent_waking' as SleepDisturbance
      if (d === 'snoring') return 'none' as SleepDisturbance
      if (d === 'other') return 'none' as SleepDisturbance
      if (allowed.has(d)) return d as SleepDisturbance
      return 'none' as SleepDisturbance
    })
    .filter((v, i, arr) => arr.indexOf(v) === i)
}

function mapActivityType(t: string): ActivityType | null {
  switch (t) {
    case 'none':
      return null
    case 'walk':
      return 'walking' as ActivityType
    case 'run':
      return 'running' as ActivityType
    case 'gym':
      return 'gym' as ActivityType
    case 'yoga':
      return 'yoga' as ActivityType
    case 'cycling':
      return 'cycling' as ActivityType
    case 'sports':
      return 'sports' as ActivityType
    case 'housework':
      return 'other' as ActivityType
    case 'other':
      return 'other' as ActivityType
    default:
      return null
  }
}

function mapSocialFrequency(f: string): SocialFrequency {
  switch (f) {
    case 'none':
      return 'none' as SocialFrequency
    case 'once':
    case 'rare':
      return 'rare' as SocialFrequency
    case 'few':
    case 'occasional':
      return 'occasional' as SocialFrequency
    case 'several':
    case 'frequent':
      return 'frequent' as SocialFrequency
    case 'daily':
      return 'daily' as SocialFrequency
    default:
      return 'none' as SocialFrequency
  }
}

function mapSymptomType(key: string): SymptomType | null {
  switch (key) {
    case 'anxiety':
    case 'panic':
      return 'panic' as SymptomType
    case 'depression':
    case 'low_mood':
      return 'low_mood' as SymptomType
    case 'sleep':
      return 'fatigue' as SymptomType
    case 'appetite':
      return 'appetite_change' as SymptomType
    case 'energy':
    case 'fatigue':
      return 'fatigue' as SymptomType
    case 'concentration':
      return 'concentration' as SymptomType
    case 'irritability':
      return 'irritability' as SymptomType
    case 'pain':
      return 'restlessness' as SymptomType
    default:
      return null
  }
}

function buildPayload(): CreateDailyLogInput {
  const activeSymptoms = form.symptoms
    .filter((s) => s.active)
    .map((s) => {
      const t = mapSymptomType(s.key)
      return t ? { type: t, severity: Math.min(5, Math.max(1, s.severity)) } : null
    })
    .filter((x): x is { type: SymptomType; severity: number } => x !== null)

  const today = new Date()
  const logDate = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`

  return {
    logDate,
    moodRating: form.mood ?? 3,
    anxietyLevel: Math.min(10, Math.max(1, form.anxiety)),
    stressLevel: Math.min(10, Math.max(1, form.stress)),
    sleepHours: Math.min(24, Math.max(0, form.sleepHours)),
    sleepQuality: Math.min(5, Math.max(1, form.sleepQuality)),
    sleepDisturbances: mapSleepDisturbances(form.sleepDisturbances),
    activityType: mapActivityType(form.activityType),
    activityMinutes:
      form.activityType === 'none' || form.activityMinutes <= 0
        ? null
        : Math.min(600, Math.max(0, Math.round(form.activityMinutes))),
    socialFrequency: mapSocialFrequency(form.socialFrequency),
    symptoms: activeSymptoms,
    notes: form.notes.trim().length > 0 ? form.notes.trim().slice(0, 1000) : null,
  }
}

async function handleSubmit() {
  if (isSubmitting.value) return
  isSubmitting.value = true
  try {
    await createLog(buildPayload())
    success('Thanks for logging today!')
    emit('submit', { ...form, symptoms: form.symptoms.map((s) => ({ ...s })) })
    emit('saved')
    resetForm()
    emit('close')
  } catch (err) {
    if (err instanceof ApiError) {
      toastError(err.message || 'Failed to save log')
    } else {
      toastError('Failed to save log')
    }
    // No cerramos el modal ante error
  } finally {
    isSubmitting.value = false
  }
}

function submit() {
  handleSubmit()
}

function close() {
  emit('close')
}
</script>

<template>
  <Modal :open="open" :title="title" @close="close">
    <template #default>
      <div class="flex flex-col gap-6">
        <div class="flex flex-col gap-2">
          <div class="flex items-center justify-between text-xs text-ink-500">
            <span>Step {{ currentStep + 1 }} of {{ steps.length }}</span>
            <span>{{ steps[currentStep] }}</span>
          </div>
          <div class="h-2 w-full overflow-hidden rounded-full bg-ink-100">
            <div
              class="h-full bg-clearsky-600 transition-all"
              :style="{ width: `${progress}%` }"
            />
          </div>
        </div>

        <form
          class="flex flex-col gap-6"
          @submit.prevent="submit"
        >
          <section v-if="currentStep === 0" class="flex flex-col gap-6">
            <h2 class="text-base font-medium text-ink-900">Mood</h2>
            <MoodScale
              v-model="form.mood"
              aria-label="Mood"
            />
            <SliderField
              v-model="form.anxiety"
              label="Anxiety"
              :min="1"
              :max="10"
              :step="1"
              helper-text="1 = very low, 10 = very high"
            />
            <SliderField
              v-model="form.stress"
              label="Stress"
              :min="1"
              :max="10"
              :step="1"
              helper-text="1 = very low, 10 = very high"
            />
          </section>

          <section v-if="currentStep === 1" class="flex flex-col gap-6">
            <h2 class="text-base font-medium text-ink-900">Sleep</h2>
            <SliderField
              v-model="form.sleepHours"
              label="Sleep hours"
              :min="0"
              :max="24"
              :step="0.5"
              helper-text="Approximate hours of sleep (0–24)"
            />
            <SliderField
              v-model="form.sleepQuality"
              label="Sleep quality"
              :min="1"
              :max="5"
              :step="1"
              helper-text="1 = very poor, 5 = very good"
            />
            <fieldset class="flex flex-col gap-3 rounded-2xl border border-ink-200 bg-paper-50 p-4">
              <legend class="px-1 text-sm font-medium text-ink-900">Sleep disturbances (select all that apply)</legend>
              <div class="grid gap-2 sm:grid-cols-2">
                <label
                  v-for="opt in sleepDisturbanceOptions"
                  :key="opt.key"
                  class="flex cursor-pointer items-center gap-2 rounded-lg border border-transparent px-2 py-2 hover:bg-ink-50"
                >
                  <input
                    type="checkbox"
                    :value="opt.key"
                    v-model="form.sleepDisturbances"
                    class="h-4 w-4 accent-clearsky-600 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-clearsky-500"
                  />
                  <span class="text-sm text-ink-900">{{ opt.label }}</span>
                </label>
              </div>
            </fieldset>
          </section>

          <section v-if="currentStep === 2" class="flex flex-col gap-6">
            <h2 class="text-base font-medium text-ink-900">Activity and social life</h2>

            <div class="flex flex-col gap-2">
              <label for="activityType" class="text-sm font-medium text-ink-900">Activity type</label>
              <select
                id="activityType"
                v-model="form.activityType"
                class="rounded-lg border border-ink-200 bg-paper-50 px-3 py-2 text-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-clearsky-500"
              >
                <option v-for="opt in activityTypeOptions" :key="opt.key" :value="opt.key">
                  {{ opt.label }}
                </option>
              </select>
            </div>

            <SliderField
              v-model="form.activityMinutes"
              label="Activity minutes"
              :min="0"
              :max="600"
              :step="5"
              :disabled="form.activityType === 'none'"
              helper-text="0–600 minutes (disabled if no activity)"
            />

            <SliderField
              v-model="form.activity"
              label="Activity level"
              :min="0"
              :max="10"
              :step="1"
              helper-text="0 = very low, 10 = very high"
            />

            <div class="flex flex-col gap-2">
              <label for="socialFrequency" class="text-sm font-medium text-ink-900">Social contact frequency</label>
              <select
                id="socialFrequency"
                v-model="form.socialFrequency"
                class="rounded-lg border border-ink-200 bg-paper-50 px-3 py-2 text-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-clearsky-500"
              >
                <option v-for="opt in socialFrequencyOptions" :key="opt.key" :value="opt.key">
                  {{ opt.label }}
                </option>
              </select>
            </div>

            <SliderField
              v-model="form.social"
              label="Social connection"
              :min="0"
              :max="10"
              :step="1"
              helper-text="0 = isolated, 10 = well connected"
            />
          </section>

          <section v-if="currentStep === 3" class="flex flex-col gap-4">
            <h2 class="text-base font-medium text-ink-900">Symptoms</h2>
            <SymptomPicker
              v-model="form.symptoms"
              aria-label="Symptoms"
            />
            <div class="flex flex-col gap-2">
              <label for="notes" class="text-sm font-medium text-ink-900">Notes</label>
              <textarea
                id="notes"
                v-model="form.notes"
                maxlength="1000"
                rows="4"
                placeholder="Additional notes..."
                class="rounded-lg border border-ink-200 bg-paper-50 px-3 py-2 text-sm placeholder:text-ink-400 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-clearsky-500"
              />
              <div class="flex justify-end text-xs text-ink-500">
                {{ form.notes.length }}/1000
              </div>
            </div>
          </section>
        </form>
      </div>
    </template>

    <template #footer>
      <div class="flex w-full items-center justify-between gap-3">
        <Button variant="ghost" :disabled="currentStep === 0" @click="back">
          Back
        </Button>
        <div class="flex items-center gap-2">
          <Button variant="subtle" @click="close">Cancel</Button>
          <Button v-if="!isLastStep" :disabled="!canGoNext" @click="next">Next</Button>
          <Button v-if="!isLastStep" variant="subtle" type="submit" :loading="isSubmitting" @click="submit">Save now</Button>
          <Button v-else type="submit" :loading="isSubmitting" @click="submit">Save</Button>
        </div>
      </div>
    </template>
  </Modal>
</template>
