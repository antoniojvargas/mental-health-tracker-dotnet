<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import Modal from '../ui/Modal.vue'
import Button from '../ui/Button.vue'
import Skeleton from '../ui/Skeleton.vue'
import MoodScale from './MoodScale.vue'
import SliderField from './SliderField.vue'
import SymptomPicker from './SymptomPicker.vue'
import {
  ACTIVITY_TYPE_OPTIONS,
  SLEEP_DISTURBANCE_OPTIONS,
  SOCIAL_FREQUENCY_OPTIONS,
  createDefaultSymptoms,
  type SymptomEntry,
} from './daily-log-options'
import { create as createLog, today as getToday } from '../../services/logs.api'
import { todayIso } from '../../utils/date'
import { describeError } from '../../services/error-messages'
import { useToast } from '../../composables/useToast'
import type {
  CreateDailyLogInput,
  ActivityType,
  SocialFrequency,
  SleepDisturbance,
  DailyLog,
} from '../../types/daily-log'

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
  (e: 'saved', log: DailyLog): void
}>()

const steps = ['Mood', 'Sleep', 'Activity and social life', 'Symptoms'] as const
type StepIndex = 0 | 1 | 2 | 3
const currentStep = ref<StepIndex>(0)
const isSubmitting = ref(false)
const isLoading = ref(false)
/** Mensaje si no se pudo traer el registro de hoy, o `null` si no hubo fallo. */
const loadError = ref<string | null>(null)
const { success, error: toastError } = useToast()

const form = reactive<DailyLogForm>({
  mood: null,
  anxiety: 5,
  stress: 5,
  sleepQuality: 3,
  sleepHours: 7,
  sleepDisturbances: [],
  activityType: 'none',
  activityMinutes: 0,
  socialFrequency: 'none',
  symptoms: createDefaultSymptoms(),
  notes: '',
})

export interface DailyLogForm {
  mood: number | null
  anxiety: number
  stress: number
  sleepQuality: number
  sleepHours: number
  sleepDisturbances: SleepDisturbance[]
  activityType: ActivityType
  activityMinutes: number
  socialFrequency: SocialFrequency
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

/**
 * El formulario se puede avanzar y guardar solo cuando la carga de hoy terminó
 * bien. Mientras carga o si falló, esos botones ni se pintan: no hay sobre qué
 * actuar y evitan guardar un registro a medias con los valores por defecto.
 */
const isReady = computed(() => !isLoading.value && !loadError.value)

function loadToday() {
  isLoading.value = true
  loadError.value = null
  getToday(todayIso())
    .then((log: DailyLog | null) => {
      if (log) {
        fillFromLog(log)
      } else {
        resetForm()
      }
    })
    .catch((thrown: unknown) => {
      // No se pudo leer el registro de hoy: en vez del formulario se muestra el
      // aviso con reintento, así que nadie edita sobre datos que no llegaron.
      loadError.value = describeError(thrown)
      resetForm()
    })
    .finally(() => {
      isLoading.value = false
    })
}

function fillFromLog(log: DailyLog) {
  form.mood = log.moodRating
  form.anxiety = log.anxietyLevel
  form.stress = log.stressLevel
  form.sleepQuality = log.sleepQuality
  form.sleepHours = log.sleepHours
  form.sleepDisturbances = (log.sleepDisturbances || []).filter((d) => d !== 'none')
  form.activityType = log.activityType ?? 'none'
  form.activityMinutes = log.activityMinutes ?? 0
  form.socialFrequency = log.socialFrequency
  form.symptoms = createDefaultSymptoms().map((d) => {
    const found = (log.symptoms || []).find((s) => s.type === d.key)
    return found ? { ...d, active: true, severity: Math.min(5, Math.max(1, found.severity)) } : d
  })
  form.notes = log.notes ?? ''
  currentStep.value = 0
}

watch(
  () => props.open,
  (open) => {
    if (open) {
      currentStep.value = 0
      loadToday()
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

function resetForm() {
  form.mood = null
  form.anxiety = 5
  form.stress = 5
  form.sleepQuality = 3
  form.sleepHours = 7
  form.sleepDisturbances = []
  form.activityType = 'none'
  form.activityMinutes = 0
  form.socialFrequency = 'none'
  form.symptoms = createDefaultSymptoms()
  form.notes = ''
  currentStep.value = 0
}

function buildPayload(): CreateDailyLogInput {
  const activeSymptoms = form.symptoms
    .filter((s) => s.active)
    .map((s) => ({ type: s.key, severity: Math.min(5, Math.max(1, s.severity)) }))

  return {
    logDate: todayIso(),
    moodRating: form.mood ?? 3,
    anxietyLevel: Math.min(10, Math.max(1, form.anxiety)),
    stressLevel: Math.min(10, Math.max(1, form.stress)),
    sleepHours: Math.min(24, Math.max(0, form.sleepHours)),
    sleepQuality: Math.min(5, Math.max(1, form.sleepQuality)),
    sleepDisturbances: form.sleepDisturbances,
    activityType: form.activityType,
    activityMinutes:
      form.activityType === 'none' || form.activityMinutes <= 0
        ? null
        : Math.min(600, Math.max(0, Math.round(form.activityMinutes))),
    socialFrequency: form.socialFrequency,
    symptoms: activeSymptoms,
    notes: form.notes.trim().length > 0 ? form.notes.trim().slice(0, 1000) : null,
  }
}

async function handleSubmit() {
  if (isSubmitting.value) return
  isSubmitting.value = true
  try {
    const savedLog = await createLog(buildPayload())
    success('Thanks for logging today!')
    emit('submit', { ...form, symptoms: form.symptoms.map((s) => ({ ...s })) })
    emit('saved', savedLog)
    resetForm()
    emit('close')
  } catch (thrown) {
    // Mensaje traducido desde el `code`, nunca `thrown.message`: lo que la API
    // mande en `message` puede ser jerga o detalle interno y acabaría en pantalla.
    toastError(describeError(thrown))
    // No cerramos el modal ante error: lo que se escribió sigue ahí para reintentar.
  } finally {
    isSubmitting.value = false
  }
}

function onSubmit() {
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
        <!--
          La lectura de hoy falló. Se ofrece reintentar en el sitio en vez de
          abrir el formulario vacío como si tal cosa: sin este aviso, un fallo de
          red se leería como "no había nada registrado".
        -->
        <div v-if="loadError" class="flex flex-col items-start gap-3">
          <p role="alert" class="text-sm leading-relaxed text-ink-600">{{ loadError }}</p>
          <Button variant="subtle" @click="loadToday">Try again</Button>
        </div>

        <div v-else class="flex flex-col gap-2">
          <div class="flex items-center justify-between text-xs text-ink-500">
            <!--
              Región viva: al pasar de paso cambia el texto pero el foco se queda
              en el botón, así que sin esto el avance no se anuncia. La barra de
              progreso de abajo es el mismo dato en dibujo, y por eso va
              `aria-hidden`: repetirlo sería anunciar dos veces lo mismo.
            -->
            <!--
              Mientras carga el registro de hoy el formulario ya está en el DOM: el
              foco inicial debe caer en el primer campo, no en un botón, así que la
              espera no puede taparlo. Se anuncia en esta línea y en los botones de
              avanzar/guardar, que quedan deshabilitados hasta que la carga termina.
            -->
            <Skeleton v-if="isLoading" class="h-3 w-40" />
            <span v-if="isLoading" role="status" class="sr-only">
              We're getting today's entry ready…
            </span>
            <span v-else role="status">
              Step {{ currentStep + 1 }} of {{ steps.length }}: {{ steps[currentStep] }}
            </span>
          </div>
          <div aria-hidden="true" class="h-2 w-full overflow-hidden rounded-full bg-ink-100">
            <div class="h-full bg-clearsky-700 transition-all" :style="{ width: `${progress}%` }" />
          </div>
        </div>

        <form v-if="!loadError" class="flex flex-col gap-6" @submit.prevent="onSubmit">
          <section v-if="currentStep === 0" class="flex flex-col gap-6">
            <h3 class="text-base font-medium text-ink-700">Mood</h3>
            <MoodScale v-model="form.mood" aria-label="Mood" aria-required="true" />
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
            <h3 class="text-base font-medium text-ink-700">Sleep</h3>
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
                    v-model="form.sleepDisturbances"
                    type="checkbox"
                    :value="opt.key"
                    class="h-5 w-5 accent-clearsky-600 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-clearsky-500"
                  />
                  <span class="text-sm text-ink-700">{{ opt.label }}</span>
                </label>
              </div>
            </fieldset>
          </section>

          <section v-if="currentStep === 2" class="flex flex-col gap-6">
            <h3 class="text-base font-medium text-ink-700">Activity and social life</h3>

            <div class="flex flex-col gap-2">
              <label for="activityType" class="text-sm font-medium text-ink-700"
                >Activity type</label
              >
              <select
                id="activityType"
                v-model="form.activityType"
                class="min-h-11 rounded-lg border border-ink-200 bg-paper-50 px-3 py-2 text-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-clearsky-500"
              >
                <option v-for="opt in ACTIVITY_TYPE_OPTIONS" :key="opt.key" :value="opt.key">
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

            <div class="flex flex-col gap-2">
              <label for="socialFrequency" class="text-sm font-medium text-ink-700"
                >Social contact frequency</label
              >
              <select
                id="socialFrequency"
                v-model="form.socialFrequency"
                class="min-h-11 rounded-lg border border-ink-200 bg-paper-50 px-3 py-2 text-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-clearsky-500"
              >
                <option v-for="opt in SOCIAL_FREQUENCY_OPTIONS" :key="opt.key" :value="opt.key">
                  {{ opt.label }}
                </option>
              </select>
            </div>
          </section>

          <section v-if="currentStep === 3" class="flex flex-col gap-4">
            <h3 class="text-base font-medium text-ink-700">Symptoms</h3>
            <SymptomPicker v-model="form.symptoms" aria-label="Symptoms" />
            <div class="flex flex-col gap-2">
              <label for="notes" class="text-sm font-medium text-ink-700">Notes</label>
              <textarea
                id="notes"
                v-model="form.notes"
                maxlength="1000"
                rows="4"
                placeholder="Additional notes..."
                class="rounded-lg border border-ink-200 bg-paper-50 px-3 py-2 text-sm placeholder:text-ink-400 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-clearsky-500"
              />
              <div class="flex justify-end text-xs text-ink-500">{{ form.notes.length }}/1000</div>
            </div>
          </section>
        </form>
      </div>
    </template>

    <template #actions>
      <div class="flex w-full flex-wrap items-center justify-between gap-2">
        <Button variant="ghost" :disabled="currentStep === 0 || !isReady" @click="back">
          Back
        </Button>
        <div class="flex flex-wrap items-center justify-end gap-2">
          <Button variant="subtle" @click="close">Cancel</Button>
          <template v-if="isReady">
            <Button v-if="!isLastStep" :disabled="!canGoNext" @click="next">Next</Button>
            <Button
              v-if="!isLastStep"
              variant="subtle"
              type="submit"
              :loading="isSubmitting"
              @click="onSubmit"
              >Save now</Button
            >
            <Button v-else type="submit" :loading="isSubmitting" @click="onSubmit">Save</Button>
          </template>
        </div>
      </div>
    </template>
  </Modal>
</template>
