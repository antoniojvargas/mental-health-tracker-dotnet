/**
 * Opciones del formulario de registro diario: etiquetas y orden de cada selector.
 *
 * Los valores salen de los tipos de `types/daily-log.ts` (espejo de los enums del backend).
 * Cada tabla de etiquetas es un `Record<Tipo, string>`, así que si el backend añade un valor
 * a un enum y se actualiza `daily-log.ts`, TypeScript falla aquí hasta que el valor tenga
 * etiqueta (y falla igual si sobra una clave). El orden de las claves de cada tabla es el
 * orden en que se muestran las opciones en el formulario.
 */

import type {
  ActivityType,
  SleepDisturbance,
  SocialFrequency,
  SymptomType,
} from '../../types/daily-log'
import type { SymptomEntry } from '../../types/daily-log-form'

export interface Option<T extends string> {
  /** Valor del enum: es la clave estable y lo que viaja a la API. */
  key: T
  /** Etiqueta legible. */
  label: string
}

function toOptions<T extends string>(labels: Record<T, string>): readonly Option<T>[] {
  return (Object.keys(labels) as T[]).map((key) => ({ key, label: labels[key] }))
}

/** `none` no es una opción: «sin alteraciones» es no marcar ninguna casilla. */
type SelectableSleepDisturbance = Exclude<SleepDisturbance, 'none'>

const SLEEP_DISTURBANCE_LABELS: Record<SelectableSleepDisturbance, string> = {
  insomnia: 'Trouble falling asleep',
  frequent_waking: 'Waking up during the night',
  early_waking: 'Waking up too early',
  nightmares: 'Nightmares',
}

const ACTIVITY_TYPE_LABELS: Record<ActivityType, string> = {
  none: 'None',
  walking: 'Walk',
  running: 'Run',
  gym: 'Gym',
  yoga: 'Yoga/Stretching',
  cycling: 'Cycling',
  sports: 'Sports',
  other: 'Other',
}

const SOCIAL_FREQUENCY_LABELS: Record<SocialFrequency, string> = {
  none: 'None',
  rare: 'Once',
  occasional: 'A few times',
  frequent: 'Several times',
  daily: 'Daily',
}

export const SYMPTOM_LABELS: Record<SymptomType, string> = {
  panic: 'Panic',
  low_mood: 'Low mood',
  fatigue: 'Fatigue',
  irritability: 'Irritability',
  concentration: 'Concentration',
  appetite_change: 'Appetite change',
  restlessness: 'Restlessness',
  hopelessness: 'Hopelessness',
}

export const SLEEP_DISTURBANCE_OPTIONS = toOptions(SLEEP_DISTURBANCE_LABELS)

export const ACTIVITY_TYPE_OPTIONS = toOptions(ACTIVITY_TYPE_LABELS)

export const SOCIAL_FREQUENCY_OPTIONS = toOptions(SOCIAL_FREQUENCY_LABELS)

export const SYMPTOM_OPTIONS = toOptions(SYMPTOM_LABELS)

/** Una entrada inactiva por cada SymptomType, en el orden en que se muestran. */
export function createDefaultSymptoms(): SymptomEntry[] {
  return SYMPTOM_OPTIONS.map(({ key, label }) => ({ key, label, active: false, severity: 1 }))
}
