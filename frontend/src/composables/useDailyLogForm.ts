/**
 * Estado del formulario multipaso de registro diario: los valores, el paso en el que está, y la
 * lectura del registro de hoy con la que se precarga.
 *
 * No sabe de Vue más allá de la reactividad ni de eventos del modal: guardar (la llamada a la API,
 * el aviso y los emits) es orquestación y se queda en DailyLogModal.vue. La conversión entre el
 * formulario y el contrato es components/daily-log/daily-log-mapper.ts.
 */

import { computed, reactive, ref } from 'vue'

import { createDefaultSymptoms } from '../components/daily-log/daily-log-options'
import { fromDailyLog } from '../components/daily-log/daily-log-mapper'
import { describeError } from '../services/error-messages'
import { today as getToday } from '../services/logs.api'
import type { DailyLog } from '../types/daily-log'
import type { DailyLogForm } from '../types/daily-log-form'
import { todayIso } from '../utils/date'

export const FORM_STEPS = ['Mood', 'Sleep', 'Activity and social life', 'Symptoms'] as const

type StepIndex = 0 | 1 | 2 | 3

/** El formulario de un día sin registrar: sin ánimo elegido y con los valores neutros. */
export function createEmptyForm(): DailyLogForm {
  return {
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
  }
}

export function useDailyLogForm() {
  const steps = FORM_STEPS
  const form = reactive<DailyLogForm>(createEmptyForm())
  const currentStep = ref<StepIndex>(0)
  const isLoading = ref(false)
  /** Mensaje si no se pudo traer el registro de hoy, o `null` si no hubo fallo. */
  const loadError = ref<string | null>(null)

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

  /** Vuelve al formulario vacío y al primer paso. */
  function reset() {
    Object.assign(form, createEmptyForm())
    currentStep.value = 0
  }

  /** Rellena el formulario con un registro ya guardado y vuelve al primer paso. */
  function fill(log: DailyLog) {
    Object.assign(form, fromDailyLog(log))
    currentStep.value = 0
  }

  /** Lee el registro del día local: lo precarga si existe y, si no, deja el formulario vacío. */
  function loadToday() {
    currentStep.value = 0
    isLoading.value = true
    loadError.value = null
    getToday(todayIso())
      .then((log: DailyLog | null) => {
        if (log) {
          fill(log)
        } else {
          reset()
        }
      })
      .catch((thrown: unknown) => {
        // No se pudo leer el registro de hoy: en vez del formulario se muestra el
        // aviso con reintento, así que nadie edita sobre datos que no llegaron.
        loadError.value = describeError(thrown)
        reset()
      })
      .finally(() => {
        isLoading.value = false
      })
  }

  return {
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
    reset,
    fill,
    loadToday,
  }
}
