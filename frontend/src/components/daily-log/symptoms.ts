import type { SymptomType } from '../../types/daily-log'

export interface SymptomEntry {
  /** Valor del enum SymptomType: es la clave estable y lo que viaja a la API */
  key: SymptomType
  /** Etiqueta legible */
  label: string
  /** Activo */
  active: boolean
  /** Severidad 1-5 cuando está activo */
  severity: number
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

/** Una entrada inactiva por cada SymptomType, en el orden en que se muestran. */
export function defaultSymptoms(): SymptomEntry[] {
  return (Object.keys(SYMPTOM_LABELS) as SymptomType[]).map((key) => ({
    key,
    label: SYMPTOM_LABELS[key],
    active: false,
    severity: 1,
  }))
}
