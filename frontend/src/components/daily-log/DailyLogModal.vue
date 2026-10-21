<script setup lang="ts">
import { ref, watch } from 'vue'
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
} from './daily-log-options'
import { toCreateDailyLogInput } from './daily-log-mapper'
import type { DailyLogForm } from '../../types/daily-log-form'
import { create as createLog } from '../../services/logs.api'
import { todayIso } from '../../utils/date'
import { describeError } from '../../services/error-messages'
import { useDailyLogForm } from '../../composables/useDailyLogForm'
import { useToast } from '../../composables/useToast'
import type { DailyLog } from '../../types/daily-log'

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

const isSubmitting = ref(false)
const { success, error: toastError } = useToast()
const {
  form,
  steps,
  currentStep,
  progress,
  canGoNext,
  isLastStep,
  isLoading,
  loadError,
  isReady,
  next,
  back,
  reset: resetForm,
  loadToday,
} = useDailyLogForm()

watch(
  () => props.open,
  (open) => {
    if (open) loadToday()
  },
)

async function handleSubmit() {
  if (isSubmitting.value) return
  isSubmitting.value = true
  try {
    const savedLog = await createLog(toCreateDailyLogInput(form, todayIso()))
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
