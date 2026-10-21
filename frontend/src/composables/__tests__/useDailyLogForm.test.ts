import { beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('../../services/logs.api', () => ({
  today: vi.fn(),
  create: vi.fn(),
}))

import { ApiError } from '../../services/api-client'
import * as logsApi from '../../services/logs.api'
import type { DailyLog } from '../../types/daily-log'
import { FORM_STEPS, createEmptyForm, useDailyLogForm } from '../useDailyLogForm'

function makeLog(overrides: Partial<DailyLog> = {}): DailyLog {
  return {
    id: '1',
    logDate: '2026-01-01',
    moodRating: 4,
    anxietyLevel: 6,
    stressLevel: 7,
    sleepHours: 6.5,
    sleepQuality: 2,
    sleepDisturbances: ['insomnia'],
    activityType: 'cycling',
    activityMinutes: 45,
    socialFrequency: 'daily',
    symptoms: [{ type: 'panic', severity: 3 }],
    notes: 'bien',
    createdAt: '2026-01-01T00:00:00.0000000+00:00',
    updatedAt: '2026-01-01T00:00:00.0000000+00:00',
    ...overrides,
  }
}

/** Deja resolverse las promesas pendientes (la lectura de hoy encadena then/catch/finally). */
async function flush() {
  await new Promise((resolve) => setTimeout(resolve, 0))
}

describe('useDailyLogForm', () => {
  beforeEach(() => {
    vi.mocked(logsApi.today).mockReset()
  })

  describe('formulario', () => {
    it('empieza vacío: sin ánimo, valores neutros y en el primer paso', () => {
      const { form, currentStep } = useDailyLogForm()

      expect(form).toEqual(createEmptyForm())
      expect(form.mood).toBeNull()
      expect(currentStep.value).toBe(0)
    })

    it('cada formulario vacío es independiente de los anteriores', () => {
      const first = createEmptyForm()
      first.symptoms[0].active = true
      first.sleepDisturbances.push('insomnia')

      const second = createEmptyForm()

      expect(second.symptoms[0].active).toBe(false)
      expect(second.sleepDisturbances).toEqual([])
    })

    it('reset devuelve el formulario vacío y al primer paso', () => {
      const { form, currentStep, reset, fill } = useDailyLogForm()
      fill(makeLog())
      currentStep.value = 3

      reset()

      expect(form).toEqual(createEmptyForm())
      expect(currentStep.value).toBe(0)
    })

    it('fill carga el registro en el formulario y vuelve al primer paso', () => {
      const { form, currentStep, fill } = useDailyLogForm()
      currentStep.value = 2

      fill(makeLog())

      expect(form).toMatchObject({
        mood: 4,
        anxiety: 6,
        stress: 7,
        sleepHours: 6.5,
        sleepQuality: 2,
        sleepDisturbances: ['insomnia'],
        activityType: 'cycling',
        activityMinutes: 45,
        socialFrequency: 'daily',
        notes: 'bien',
      })
      expect(form.symptoms.filter((s) => s.active).map((s) => [s.key, s.severity])).toEqual([
        ['panic', 3],
      ])
      expect(currentStep.value).toBe(0)
    })

    it('fill sobre un formulario con datos no deja restos del anterior', () => {
      const { form, fill } = useDailyLogForm()
      fill(makeLog({ notes: 'primero', symptoms: [{ type: 'fatigue', severity: 5 }] }))

      fill(makeLog({ notes: null, symptoms: [] }))

      expect(form.notes).toBe('')
      expect(form.symptoms.some((s) => s.active)).toBe(false)
    })
  })

  describe('pasos', () => {
    it('tiene los cuatro pasos del formulario', () => {
      expect(useDailyLogForm().steps).toEqual(FORM_STEPS)
      expect(FORM_STEPS).toEqual(['Mood', 'Sleep', 'Activity and social life', 'Symptoms'])
    })

    it('no se puede avanzar del primer paso sin elegir el ánimo', () => {
      const { form, currentStep, canGoNext, next } = useDailyLogForm()

      expect(canGoNext.value).toBe(false)
      next()
      expect(currentStep.value).toBe(0)

      form.mood = 3

      expect(canGoNext.value).toBe(true)
      next()
      expect(currentStep.value).toBe(1)
    })

    it('desde el segundo paso se avanza sin condiciones y se para en el último', () => {
      const { form, currentStep, canGoNext, next, isLastStep } = useDailyLogForm()
      form.mood = 3

      for (let i = 0; i < 10; i++) next()

      expect(currentStep.value).toBe(3)
      expect(canGoNext.value).toBe(true)
      expect(isLastStep.value).toBe(true)
    })

    it('back retrocede y se para en el primero', () => {
      const { form, currentStep, next, back } = useDailyLogForm()
      form.mood = 3
      next()
      next()

      back()
      expect(currentStep.value).toBe(1)

      back()
      back()
      back()
      expect(currentStep.value).toBe(0)
    })

    it('isLastStep solo es cierto en el último paso', () => {
      const { form, isLastStep, next } = useDailyLogForm()
      form.mood = 3

      const seen: boolean[] = []
      for (let i = 0; i < 4; i++) {
        seen.push(isLastStep.value)
        next()
      }

      expect(seen).toEqual([false, false, false, true])
    })

    it('progress es el porcentaje de pasos alcanzados', () => {
      const { form, progress, next } = useDailyLogForm()
      form.mood = 3

      const seen: number[] = []
      for (let i = 0; i < 4; i++) {
        seen.push(progress.value)
        next()
      }

      expect(seen).toEqual([25, 50, 75, 100])
    })
  })

  describe('loadToday', () => {
    it('mientras lee no está listo, y al terminar sí', async () => {
      let resolve!: (log: DailyLog | null) => void
      vi.mocked(logsApi.today).mockReturnValue(new Promise((r) => (resolve = r)))
      const { isLoading, isReady, loadToday } = useDailyLogForm()

      loadToday()

      expect(isLoading.value).toBe(true)
      expect(isReady.value).toBe(false)

      resolve(null)
      await flush()

      expect(isLoading.value).toBe(false)
      expect(isReady.value).toBe(true)
    })

    it('devuelve una promesa que se resuelve al terminar la lectura', async () => {
      vi.mocked(logsApi.today).mockResolvedValue(makeLog({ moodRating: 2 }))
      const { form, isLoading, loadToday } = useDailyLogForm()

      await loadToday()

      expect(form.mood).toBe(2)
      expect(isLoading.value).toBe(false)
    })

    it('la promesa no se rechaza aunque la lectura falle: el fallo queda en loadError', async () => {
      vi.mocked(logsApi.today).mockRejectedValue(
        new ApiError(500, { code: 'INTERNAL_ERROR', message: 'boom', details: [] }),
      )
      const { loadError, loadToday } = useDailyLogForm()

      await expect(loadToday()).resolves.toBeUndefined()

      expect(loadError.value).not.toBeNull()
    })

    it('pide el registro del día local', async () => {
      vi.mocked(logsApi.today).mockResolvedValue(null)

      useDailyLogForm().loadToday()
      await flush()

      expect(logsApi.today).toHaveBeenCalledTimes(1)
      expect(vi.mocked(logsApi.today).mock.calls[0][0]).toMatch(/^\d{4}-\d{2}-\d{2}$/)
    })

    it('con registro de hoy lo precarga', async () => {
      vi.mocked(logsApi.today).mockResolvedValue(makeLog({ moodRating: 5 }))
      const { form, loadToday } = useDailyLogForm()

      loadToday()
      await flush()

      expect(form.mood).toBe(5)
    })

    it('sin registro de hoy deja el formulario vacío', async () => {
      vi.mocked(logsApi.today).mockResolvedValue(null)
      const { form, fill, loadToday } = useDailyLogForm()
      fill(makeLog())

      loadToday()
      await flush()

      expect(form).toEqual(createEmptyForm())
    })

    it('vuelve al primer paso al empezar a leer', async () => {
      vi.mocked(logsApi.today).mockResolvedValue(null)
      const { form, currentStep, next, loadToday } = useDailyLogForm()
      form.mood = 3
      next()
      next()

      loadToday()

      expect(currentStep.value).toBe(0)
      await flush()
    })

    it('si falla guarda el mensaje traducido, no está listo y deja el formulario vacío', async () => {
      vi.mocked(logsApi.today).mockRejectedValue(
        new ApiError(500, { code: 'INTERNAL_ERROR', message: 'boom', details: [] }),
      )
      const { form, fill, isReady, isLoading, loadError, loadToday } = useDailyLogForm()
      fill(makeLog())

      loadToday()
      await flush()

      expect(loadError.value).toBeTruthy()
      expect(loadError.value).not.toContain('boom')
      expect(isReady.value).toBe(false)
      expect(isLoading.value).toBe(false)
      expect(form).toEqual(createEmptyForm())
    })

    it('reintentar descarta el aviso anterior y, si sale bien, queda listo', async () => {
      vi.mocked(logsApi.today)
        .mockRejectedValueOnce(
          new ApiError(500, { code: 'INTERNAL_ERROR', message: 'boom', details: [] }),
        )
        .mockResolvedValueOnce(makeLog())
      const { form, isReady, loadError, loadToday } = useDailyLogForm()

      loadToday()
      await flush()
      expect(loadError.value).not.toBeNull()

      loadToday()
      expect(loadError.value).toBeNull()
      await flush()

      expect(isReady.value).toBe(true)
      expect(form.mood).toBe(4)
    })
  })
})
