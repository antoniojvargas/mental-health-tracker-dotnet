import { describe, expect, it } from 'vitest'
import { fromDailyLog, toCreateDailyLogInput } from '../daily-log-mapper'
import {
  ACTIVITY_TYPES,
  SLEEP_DISTURBANCES,
  SOCIAL_FREQUENCIES,
  SYMPTOM_TYPES,
  type ActivityType,
  type DailyLog,
  type SleepDisturbance,
  type SymptomType,
} from '../../../types/daily-log'
import { makeLog } from '../../../test-support/factories'

// Ida y vuelta: leer un registro en el formulario y volver a enviarlo sin tocar nada debe dar a
// `create` exactamente los mismos campos que el registro (A5 del Apéndice de MEJORAS.md).
//
// Es la especificación del formulario sin pérdida: ya no queda ningún `it.fails`. Si un
// cambio vuelve a perder datos, el caso correspondiente falla. No se debe borrar un caso
// para "arreglar" la suite.
describe('daily-log-mapper · ida y vuelta sin pérdida (A5)', () => {
  const ROUND_TRIP_KEYS = [
    'moodRating',
    'anxietyLevel',
    'stressLevel',
    'sleepHours',
    'sleepQuality',
    'sleepDisturbances',
    'activityType',
    'activityMinutes',
    'socialFrequency',
    'symptoms',
    'notes',
  ] as const

  /** Los campos que viajan en el POST, con los síntomas en orden estable (el orden no es contrato). */
  function roundTripFields(source: Record<string, unknown>) {
    const picked: Record<string, unknown> = {}
    for (const key of ROUND_TRIP_KEYS) {
      picked[key] = source[key]
    }
    const symptoms = (picked.symptoms as { type: string; severity: number }[]).map((s) => ({
      ...s,
    }))
    picked.symptoms = symptoms.sort((a, b) => a.type.localeCompare(b.type))
    return picked
  }

  function roundTrip(log: DailyLog) {
    return toCreateDailyLogInput(fromDailyLog(log), log.logDate)
  }

  function saveUntouchedAndCompare(log: DailyLog) {
    const sent = roundTrip(log) as unknown as Record<string, unknown>
    expect(roundTripFields(sent)).toEqual(roundTripFields({ ...log }))
  }

  it('un registro base sin enums especiales vuelve igual', () => {
    saveUntouchedAndCompare(makeLog())
  })

  describe('SocialFrequency', () => {
    for (const value of SOCIAL_FREQUENCIES) {
      it(`${value}: el payload conserva el valor`, () => {
        saveUntouchedAndCompare({ ...makeLog(), socialFrequency: value })
      })
    }
  })

  describe('ActivityType', () => {
    for (const value of ACTIVITY_TYPES) {
      it(`${value}: el payload conserva tipo y minutos`, () => {
        saveUntouchedAndCompare({
          ...makeLog(),
          activityType: value as ActivityType,
          activityMinutes: value === 'none' ? null : 30,
        })
      })
    }

    // `null` y `none` significan lo mismo para el usuario; el select solo conoce `none`.
    it('sin actividad (null): se reenvía como none y sin minutos', () => {
      const sent = roundTrip({ ...makeLog(), activityType: null, activityMinutes: null })

      expect(sent.activityType).toBe('none')
      expect(sent.activityMinutes).toBeNull()
    })
  })

  describe('SleepDisturbance', () => {
    // `none` no es una alteración: la ausencia se representa con la lista vacía (caso base).
    const disturbances = SLEEP_DISTURBANCES.filter(
      (value): value is Exclude<SleepDisturbance, 'none'> => value !== 'none',
    )

    for (const value of disturbances) {
      // `early_waking` vuelve como `frequent_waking`. A5.
      it(`${value}: el payload conserva la alteración`, () => {
        saveUntouchedAndCompare({ ...makeLog(), sleepDisturbances: [value] })
      })
    }

    // Varias a la vez. Falla por `early_waking`. A5.
    it('todas juntas: el payload conserva la lista completa', () => {
      saveUntouchedAndCompare({ ...makeLog(), sleepDisturbances: disturbances })
    })
  })

  describe('SymptomType', () => {
    SYMPTOM_TYPES.forEach((type: SymptomType) => {
      it(`${type}: conserva tipo y severidad`, () => {
        saveUntouchedAndCompare({
          ...makeLog(),
          symptoms: [{ type, severity: 4 }],
        })
      })
    })

    it('todos juntos: conserva cada tipo con su severidad', () => {
      saveUntouchedAndCompare({
        ...makeLog(),
        symptoms: SYMPTOM_TYPES.map((type, index) => ({ type, severity: (index % 5) + 1 })),
      })
    })
  })
})

describe('toCreateDailyLogInput', () => {
  it('fecha el cuerpo con el día que se le pasa, no con el del registro', () => {
    const form = fromDailyLog({ ...makeLog(), logDate: '2026-01-01' })

    expect(toCreateDailyLogInput(form, '2026-03-15').logDate).toBe('2026-03-15')
  })

  it('sin ánimo elegido envía 3', () => {
    const form = { ...fromDailyLog(makeLog()), mood: null }

    expect(toCreateDailyLogInput(form, '2026-03-15').moodRating).toBe(3)
  })

  it('acota cada valor a su rango', () => {
    const form = {
      ...fromDailyLog(makeLog()),
      anxiety: 99,
      stress: -4,
      sleepHours: 30,
      sleepQuality: 0,
      activityType: 'gym' as const,
      activityMinutes: 9999,
    }

    expect(toCreateDailyLogInput(form, '2026-03-15')).toMatchObject({
      anxietyLevel: 10,
      stressLevel: 1,
      sleepHours: 24,
      sleepQuality: 1,
      activityMinutes: 600,
    })
  })

  it('redondea los minutos y los omite sin actividad o con 0', () => {
    const base = fromDailyLog(makeLog())

    expect(
      toCreateDailyLogInput({ ...base, activityType: 'yoga', activityMinutes: 29.6 }, '2026-03-15')
        .activityMinutes,
    ).toBe(30)
    expect(
      toCreateDailyLogInput({ ...base, activityType: 'none', activityMinutes: 45 }, '2026-03-15')
        .activityMinutes,
    ).toBeNull()
    expect(
      toCreateDailyLogInput({ ...base, activityType: 'yoga', activityMinutes: 0 }, '2026-03-15')
        .activityMinutes,
    ).toBeNull()
  })

  it('recorta las notas, trunca a 1000 caracteres y envía null si quedan vacías', () => {
    const base = fromDailyLog(makeLog())

    expect(toCreateDailyLogInput({ ...base, notes: '  hola  ' }, '2026-03-15').notes).toBe('hola')
    expect(
      toCreateDailyLogInput({ ...base, notes: 'x'.repeat(1500) }, '2026-03-15').notes,
    ).toHaveLength(1000)
    expect(toCreateDailyLogInput({ ...base, notes: '   ' }, '2026-03-15').notes).toBeNull()
  })

  it('solo envía los síntomas activos y acota su severidad', () => {
    const base = fromDailyLog(makeLog())
    const symptoms = base.symptoms.map((s) =>
      s.key === 'panic'
        ? { ...s, active: true, severity: 9 }
        : s.key === 'fatigue'
          ? { ...s, active: true, severity: 0 }
          : s,
    )

    expect(toCreateDailyLogInput({ ...base, symptoms }, '2026-03-15').symptoms).toEqual([
      { type: 'panic', severity: 5 },
      { type: 'fatigue', severity: 1 },
    ])
  })
})

describe('fromDailyLog', () => {
  it('rellena cada campo del formulario desde el registro', () => {
    const form = fromDailyLog({
      ...makeLog(),
      moodRating: 4,
      anxietyLevel: 6,
      stressLevel: 7,
      sleepHours: 6.5,
      sleepQuality: 2,
      activityType: 'cycling',
      activityMinutes: 45,
      socialFrequency: 'daily',
      notes: 'bien',
    })

    expect(form).toMatchObject({
      mood: 4,
      anxiety: 6,
      stress: 7,
      sleepHours: 6.5,
      sleepQuality: 2,
      activityType: 'cycling',
      activityMinutes: 45,
      socialFrequency: 'daily',
      notes: 'bien',
    })
  })

  it('descarta none de las alteraciones del sueño y trata null como lista vacía', () => {
    expect(fromDailyLog({ ...makeLog(), sleepDisturbances: ['none'] }).sleepDisturbances).toEqual(
      [],
    )
    expect(
      fromDailyLog({ ...makeLog(), sleepDisturbances: ['none', 'insomnia'] }).sleepDisturbances,
    ).toEqual(['insomnia'])
  })

  it('activityType y activityMinutes nulos pasan a none y 0; notes nulo a cadena vacía', () => {
    const form = fromDailyLog({
      ...makeLog(),
      activityType: null,
      activityMinutes: null,
      notes: null,
    })

    expect(form).toMatchObject({ activityType: 'none', activityMinutes: 0, notes: '' })
  })

  it('deja los síntomas del registro activos con su severidad y el resto inactivos', () => {
    const { symptoms } = fromDailyLog({
      ...makeLog(),
      symptoms: [
        { type: 'low_mood', severity: 3 },
        { type: 'panic', severity: 9 },
      ],
    })

    expect(symptoms.filter((s) => s.active).map((s) => [s.key, s.severity])).toEqual([
      ['panic', 5],
      ['low_mood', 3],
    ])
    expect(symptoms).toHaveLength(SYMPTOM_TYPES.length)
    expect(symptoms.filter((s) => !s.active).every((s) => s.severity === 1)).toBe(true)
  })

  it('cada llamada devuelve un formulario independiente', () => {
    const log = makeLog()
    const first = fromDailyLog(log)
    const second = fromDailyLog(log)

    first.symptoms[0].active = true
    first.sleepDisturbances.push('insomnia')

    expect(second.symptoms[0].active).toBe(false)
    expect(second.sleepDisturbances).toEqual([])
  })
})
