import { describe, expect, it } from 'vitest'
import {
  ACTIVITY_TYPES,
  SLEEP_DISTURBANCES,
  SOCIAL_FREQUENCIES,
  SYMPTOM_TYPES,
} from '../../../types/daily-log'
import {
  ACTIVITY_TYPE_OPTIONS,
  SLEEP_DISTURBANCE_OPTIONS,
  SOCIAL_FREQUENCY_OPTIONS,
  SYMPTOM_OPTIONS,
  createDefaultSymptoms,
} from '../daily-log-options'

const keys = (options: readonly { key: string }[]) => options.map((o) => o.key)

// Las tablas son Record<Tipo, string>, así que TypeScript ya obliga a que haya una etiqueta por
// cada valor del enum. Estos tests fijan lo que el compilador no ve: que no quede ninguna opción
// de más, que el orden de pantalla no cambie sin querer y que las etiquetas no estén vacías.
describe('daily-log-options', () => {
  it('ofrece exactamente los valores de cada enum del contrato', () => {
    expect([...keys(ACTIVITY_TYPE_OPTIONS)].sort()).toEqual([...ACTIVITY_TYPES].sort())
    expect([...keys(SOCIAL_FREQUENCY_OPTIONS)].sort()).toEqual([...SOCIAL_FREQUENCIES].sort())
    expect([...keys(SYMPTOM_OPTIONS)].sort()).toEqual([...SYMPTOM_TYPES].sort())
  })

  it('las alteraciones del sueño son todas las del enum salvo none', () => {
    expect([...keys(SLEEP_DISTURBANCE_OPTIONS)].sort()).toEqual(
      SLEEP_DISTURBANCES.filter((d) => d !== 'none').sort(),
    )
  })

  it('mantiene el orden con el que se muestran las opciones', () => {
    expect(keys(SLEEP_DISTURBANCE_OPTIONS)).toEqual([
      'insomnia',
      'frequent_waking',
      'early_waking',
      'nightmares',
    ])
    expect(keys(ACTIVITY_TYPE_OPTIONS)).toEqual([
      'none',
      'walking',
      'running',
      'gym',
      'yoga',
      'cycling',
      'sports',
      'other',
    ])
    expect(keys(SOCIAL_FREQUENCY_OPTIONS)).toEqual([
      'none',
      'rare',
      'occasional',
      'frequent',
      'daily',
    ])
    expect(keys(SYMPTOM_OPTIONS)).toEqual([
      'panic',
      'low_mood',
      'fatigue',
      'irritability',
      'concentration',
      'appetite_change',
      'restlessness',
      'hopelessness',
    ])
  })

  it('todas las opciones tienen etiqueta y sin repetidos', () => {
    for (const options of [
      SLEEP_DISTURBANCE_OPTIONS,
      ACTIVITY_TYPE_OPTIONS,
      SOCIAL_FREQUENCY_OPTIONS,
      SYMPTOM_OPTIONS,
    ]) {
      expect(options.every((o) => o.label.trim().length > 0)).toBe(true)
      expect(new Set(keys(options)).size).toBe(options.length)
      expect(new Set(options.map((o) => o.label)).size).toBe(options.length)
    }
  })

  describe('createDefaultSymptoms', () => {
    it('crea una entrada inactiva con severidad 1 por cada síntoma, en el orden de pantalla', () => {
      expect(createDefaultSymptoms()).toEqual(
        SYMPTOM_OPTIONS.map(({ key, label }) => ({ key, label, active: false, severity: 1 })),
      )
    })

    it('devuelve una lista nueva cada vez, sin compartir objetos', () => {
      const first = createDefaultSymptoms()
      const second = createDefaultSymptoms()

      first[0].active = true
      first[0].severity = 5

      expect(second[0]).toMatchObject({ active: false, severity: 1 })
      expect(second).not.toBe(first)
      expect(second[0]).not.toBe(first[0])
    })
  })
})
