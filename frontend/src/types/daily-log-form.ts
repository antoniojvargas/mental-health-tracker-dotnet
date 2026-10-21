/**
 * Estado del formulario de registro diario.
 *
 * Son tipos de la interfaz, no del contrato con la API: `daily-log.ts` describe lo que viaja por
 * HTTP; aquí está la forma en que el modal guarda lo que el usuario escribe (el ánimo aún sin
 * elegir, los síntomas con su casilla y su severidad, los minutos como número). La conversión
 * entre una y otra es components/daily-log/daily-log-mapper.ts.
 */

import type { ActivityType, SleepDisturbance, SocialFrequency, SymptomType } from './daily-log'

/** Un síntoma del formulario: la opción más su estado de activación y severidad. */
export interface SymptomEntry {
  /** Valor del enum SymptomType: es la clave estable y lo que viaja a la API. */
  key: SymptomType
  /** Etiqueta legible. */
  label: string
  /** Activo. */
  active: boolean
  /** Severidad 1-5 cuando está activo. */
  severity: number
}

export interface DailyLogForm {
  /** `null` hasta que el usuario elige: sin ánimo no se puede avanzar. */
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
