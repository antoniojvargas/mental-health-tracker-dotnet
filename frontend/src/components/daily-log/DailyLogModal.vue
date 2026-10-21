<script setup lang="ts">
import { ref, watch } from 'vue'
import Modal from '../ui/Modal.vue'
import Button from '../ui/Button.vue'
import Skeleton from '../ui/Skeleton.vue'
import MoodStep from './steps/MoodStep.vue'
import SleepStep from './steps/SleepStep.vue'
import ActivitySocialStep from './steps/ActivitySocialStep.vue'
import SymptomsStep from './steps/SymptomsStep.vue'
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
    if (open) void loadToday()
  },
)

async function onSubmit() {
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
          <MoodStep
            v-if="currentStep === 0"
            v-model:mood="form.mood"
            v-model:anxiety="form.anxiety"
            v-model:stress="form.stress"
          />
          <SleepStep
            v-if="currentStep === 1"
            v-model:sleep-hours="form.sleepHours"
            v-model:sleep-quality="form.sleepQuality"
            v-model:sleep-disturbances="form.sleepDisturbances"
          />
          <ActivitySocialStep
            v-if="currentStep === 2"
            v-model:activity-type="form.activityType"
            v-model:activity-minutes="form.activityMinutes"
            v-model:social-frequency="form.socialFrequency"
          />
          <SymptomsStep
            v-if="currentStep === 3"
            v-model:symptoms="form.symptoms"
            v-model:notes="form.notes"
          />
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
