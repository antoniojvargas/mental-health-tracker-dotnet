import { describe, expect, it } from 'vitest'

import { getMetric, METRICS } from '../metrics'
import type { DailyLog } from '../../../types/daily-log'

function makeLog(overrides: Partial<DailyLog> = {}): DailyLog {
  return {
    id: 'log-1',
    logDate: '2026-08-01',
    moodRating: 3,
    anxietyLevel: 4,
    stressLevel: 5,
    sleepHours: 7.5,
    sleepQuality: 3,
    sleepDisturbances: [],
    activityType: null,
    activityMinutes: null,
    socialFrequency: 'occasional',
    symptoms: [],
    notes: null,
    createdAt: '2026-08-01T00:00:00.000Z',
    updatedAt: '2026-08-01T00:00:00.000Z',
    ...overrides,
  }
}

describe('METRICS', () => {
  it('define las siete métricas del dashboard en orden', () => {
    expect(METRICS.map((metric) => metric.key)).toEqual([
      'mood',
      'anxiety',
      'stress',
      'sleepHours',
      'sleepQuality',
      'activityMinutes',
      'symptomLoad',
    ])
  })
})

describe('getMetric', () => {
  it('devuelve la métrica de la clave pedida', () => {
    expect(getMetric('anxiety').label).toBe('Anxiety')
  })

  it('cae a la primera métrica si la clave no existe', () => {
    expect(getMetric('does-not-exist')).toBe(METRICS[0])
  })
})

describe('mood', () => {
  const mood = getMetric('mood')

  it('extrae moodRating del registro', () => {
    expect(mood.format(makeLog({ moodRating: 5 }))).toBe(5)
  })

  it('describe la escala 1-5 en lenguaje natural', () => {
    expect(mood.describe(1)).toBe('very low')
    expect(mood.describe(3)).toBe('neutral')
    expect(mood.describe(5)).toBe('very good')
  })

  it('acota valores fuera de rango en vez de devolver undefined', () => {
    expect(mood.describe(0)).toBe('very low')
    expect(mood.describe(99)).toBe('very good')
  })
})

describe('anxiety', () => {
  const anxiety = getMetric('anxiety')

  it('extrae anxietyLevel y lo describe sobre 10', () => {
    expect(anxiety.format(makeLog({ anxietyLevel: 4 }))).toBe(4)
    expect(anxiety.describe(4)).toBe('4/10')
  })
})

describe('stress', () => {
  const stress = getMetric('stress')

  it('extrae stressLevel y lo describe sobre 10', () => {
    expect(stress.format(makeLog({ stressLevel: 5 }))).toBe(5)
    expect(stress.describe(5)).toBe('5/10')
  })
})

describe('sleepHours', () => {
  const sleepHours = getMetric('sleepHours')

  it('va en el eje de horas, no en el eje de escala', () => {
    expect(sleepHours.axis).toBe('hours')
  })

  it('extrae sleepHours y le pone la unidad', () => {
    expect(sleepHours.format(makeLog({ sleepHours: 7.5 }))).toBe(7.5)
    expect(sleepHours.describe(7.5)).toBe('7.5 h')
  })
})

describe('sleepQuality', () => {
  const sleepQuality = getMetric('sleepQuality')

  it('extrae sleepQuality y describe la escala 1-5', () => {
    expect(sleepQuality.format(makeLog({ sleepQuality: 3 }))).toBe(3)
    expect(sleepQuality.describe(1)).toBe('very poor')
    expect(sleepQuality.describe(5)).toBe('very good')
  })
})

describe('activityMinutes', () => {
  const activityMinutes = getMetric('activityMinutes')

  it('trata activityMinutes null como cero, no como NaN', () => {
    expect(activityMinutes.format(makeLog({ activityMinutes: null }))).toBe(0)
  })

  it('extrae los minutos y los describe con su unidad', () => {
    expect(activityMinutes.format(makeLog({ activityMinutes: 45 }))).toBe(45)
    expect(activityMinutes.describe(45)).toBe('45 min')
  })
})

describe('symptomLoad', () => {
  const symptomLoad = getMetric('symptomLoad')

  it('es cero y se lee "no symptoms" cuando no hay síntomas', () => {
    const log = makeLog({ symptoms: [] })
    expect(symptomLoad.format(log)).toBe(0)
    expect(symptomLoad.describe(symptomLoad.format(log))).toBe('no symptoms')
  })

  it('promedia la severidad de todos los síntomas del día', () => {
    const log = makeLog({
      symptoms: [
        { type: 'fatigue', severity: 2 },
        { type: 'irritability', severity: 4 },
      ],
    })
    expect(symptomLoad.format(log)).toBe(3)
    expect(symptomLoad.describe(3)).toBe('3.0/5')
  })
})
