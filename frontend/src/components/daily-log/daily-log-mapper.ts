/**
 * Conversión pura entre el formulario del modal y el contrato de la API.
 *
 * `toCreateDailyLogInput` es la ida (formulario → cuerpo de POST /api/logs) y `fromDailyLog` la
 * vuelta (registro de la API → formulario). Sin Vue ni red: se prueban en entorno node, y que
 * `toCreateDailyLogInput(fromDailyLog(log), log.logDate)` reenvíe lo mismo que se recibió es la
 * especificación del formulario sin pérdida (daily-log-mapper.test.ts).
 */

import type {
  ActivityType,
  CreateDailyLogInput,
  DailyLog,
  IsoDate,
  SleepDisturbance,
  SocialFrequency,
} from '../../types/daily-log'
import { createDefaultSymptoms, type SymptomEntry } from './daily-log-options'

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

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value))
}

/** El cuerpo de POST /api/logs para el formulario, fechado en `today` (el día local del cliente). */
export function toCreateDailyLogInput(form: DailyLogForm, today: IsoDate): CreateDailyLogInput {
  const activeSymptoms = form.symptoms
    .filter((s) => s.active)
    .map((s) => ({ type: s.key, severity: clamp(s.severity, 1, 5) }))

  return {
    logDate: today,
    moodRating: form.mood ?? 3,
    anxietyLevel: clamp(form.anxiety, 1, 10),
    stressLevel: clamp(form.stress, 1, 10),
    sleepHours: clamp(form.sleepHours, 0, 24),
    sleepQuality: clamp(form.sleepQuality, 1, 5),
    sleepDisturbances: form.sleepDisturbances,
    activityType: form.activityType,
    activityMinutes:
      form.activityType === 'none' || form.activityMinutes <= 0
        ? null
        : clamp(Math.round(form.activityMinutes), 0, 600),
    socialFrequency: form.socialFrequency,
    symptoms: activeSymptoms,
    notes: form.notes.trim().length > 0 ? form.notes.trim().slice(0, 1000) : null,
  }
}

/** El formulario que corresponde a un registro ya guardado. */
export function fromDailyLog(log: DailyLog): DailyLogForm {
  return {
    mood: log.moodRating,
    anxiety: log.anxietyLevel,
    stress: log.stressLevel,
    sleepQuality: log.sleepQuality,
    sleepHours: log.sleepHours,
    // `none` no es una casilla: «sin alteraciones» es la lista vacía.
    sleepDisturbances: (log.sleepDisturbances || []).filter((d) => d !== 'none'),
    activityType: log.activityType ?? 'none',
    activityMinutes: log.activityMinutes ?? 0,
    socialFrequency: log.socialFrequency,
    symptoms: createDefaultSymptoms().map((entry) => {
      const found = (log.symptoms || []).find((s) => s.type === entry.key)
      return found ? { ...entry, active: true, severity: clamp(found.severity, 1, 5) } : entry
    }),
    notes: log.notes ?? '',
  }
}
