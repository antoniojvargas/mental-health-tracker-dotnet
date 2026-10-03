import { beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { useLogsStore } from './useLogsStore'
import { jsonResponse, mockFetch } from '../test-support/http'
import type { DailyLog, IsoDate } from '../types/daily-log'

function makeLog(logDate: IsoDate): DailyLog {
  return {
    id: `id-${logDate}`,
    logDate,
    moodRating: 3,
    anxietyLevel: 5,
    stressLevel: 5,
    sleepHours: 7,
    sleepQuality: 4,
    sleepDisturbances: [],
    activityType: null,
    activityMinutes: null,
    socialFrequency: 'none',
    symptoms: [],
    notes: null,
    createdAt: '2026-09-13T17:18:21.1234567+00:00',
    updatedAt: '2026-09-13T17:18:21.1234567+00:00',
  }
}

function listResponse(data: DailyLog[]) {
  return jsonResponse({
    data,
    meta: { from: '2026-01-01', to: '2026-12-31', limit: 100, offset: 0, total: data.length },
  })
}

/** Días entre dos "yyyy-MM-dd", para no acoplar el test a fechas fijas. */
function daysBetween(from: IsoDate, to: IsoDate): number {
  const [fy, fm, fd] = from.split('-').map(Number)
  const [ty, tm, td] = to.split('-').map(Number)
  const fromDate = new Date(fy, fm - 1, fd)
  const toDate = new Date(ty, tm - 1, td)
  return Math.round((toDate.getTime() - fromDate.getTime()) / 86_400_000)
}

describe('useLogsStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('pide los últimos 7 días (hoy incluido) y guarda los logs ordenados', async () => {
    const fetchSpy = mockFetch(listResponse([makeLog('2026-10-03'), makeLog('2026-09-30')]))
    const store = useLogsStore()

    const pending = store.fetch('week')
    expect(store.loading).toBe(true)
    await pending

    const url = new URL(fetchSpy.mock.calls[0][0] as string, 'http://localhost')
    const from = url.searchParams.get('from') as IsoDate
    const to = url.searchParams.get('to') as IsoDate
    expect(daysBetween(from, to)).toBe(6)

    expect(store.logs.map((log) => log.logDate)).toEqual(['2026-09-30', '2026-10-03'])
    expect(store.error).toBeNull()
    expect(store.loading).toBe(false)
  })

  it('pide los últimos 30 días cuando el rango es "month"', async () => {
    const fetchSpy = mockFetch(listResponse([]))
    const store = useLogsStore()

    await store.fetch('month')

    const url = new URL(fetchSpy.mock.calls[0][0] as string, 'http://localhost')
    const from = url.searchParams.get('from') as IsoDate
    const to = url.searchParams.get('to') as IsoDate
    expect(daysBetween(from, to)).toBe(29)
  })

  it('guarda el mensaje de error y no relanza, sin vaciar los logs ya cargados', async () => {
    mockFetch(listResponse([makeLog('2026-10-03')]))
    const store = useLogsStore()
    await store.fetch('week')

    mockFetch(
      jsonResponse(
        { error: { code: 'INTERNAL_ERROR', message: 'Algo se rompió', details: [] } },
        500,
      ),
    )
    // No relanza: la vista lee `error` en vez de envolver la llamada.
    await store.fetch('month')

    expect(store.error).toBe('Algo se rompió')
    expect(store.loading).toBe(false)
    expect(store.logs).toHaveLength(1)
  })

  it('mergeLog inserta un registro nuevo conservando el orden ascendente', async () => {
    mockFetch(listResponse([makeLog('2026-10-01'), makeLog('2026-10-03')]))
    const store = useLogsStore()
    await store.fetch('week')

    store.mergeLog(makeLog('2026-10-02'))

    expect(store.logs.map((log) => log.logDate)).toEqual(['2026-10-01', '2026-10-02', '2026-10-03'])
  })

  it('mergeLog reemplaza el registro del mismo logDate sin duplicar', async () => {
    mockFetch(listResponse([makeLog('2026-10-01'), makeLog('2026-10-02')]))
    const store = useLogsStore()
    await store.fetch('week')

    const edited = { ...makeLog('2026-10-02'), moodRating: 5 }
    store.mergeLog(edited)

    expect(store.logs).toHaveLength(2)
    expect(store.logs.find((log) => log.logDate === '2026-10-02')).toEqual(edited)
  })

  it('mergeLog funciona sin una carga previa', () => {
    const store = useLogsStore()

    store.mergeLog(makeLog('2026-10-02'))
    store.mergeLog(makeLog('2026-10-01'))

    expect(store.logs.map((log) => log.logDate)).toEqual(['2026-10-01', '2026-10-02'])
  })
})
