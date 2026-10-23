import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { useLogsStore } from './useLogsStore'
import { jsonResponse, mockFetch } from '../test-support/http'
import type { DailyLog, IsoDate } from '../types/daily-log'
import { makeLog } from '../test-support/factories'

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
    const fetchSpy = mockFetch(
      listResponse([makeLog({ logDate: '2026-10-03' }), makeLog({ logDate: '2026-09-30' })]),
    )
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

  it('al cambiar de rango vuelve a pedir la ventana y reemplaza la lista', async () => {
    mockFetch(listResponse([makeLog({ logDate: '2026-10-01' })]))
    const store = useLogsStore()
    await store.fetch('week')
    expect(store.logs.map((log) => log.logDate)).toEqual(['2026-10-01'])

    const fetchSpy = mockFetch(
      listResponse([makeLog({ logDate: '2026-09-05' }), makeLog({ logDate: '2026-10-01' })]),
    )
    await store.fetch('month')

    const url = new URL(fetchSpy.mock.calls[0][0] as string, 'http://localhost')
    const from = url.searchParams.get('from') as IsoDate
    const to = url.searchParams.get('to') as IsoDate
    expect(daysBetween(from, to)).toBe(29)
    // Reemplaza, no acumula: la lista es la ventana nueva.
    expect(store.logs.map((log) => log.logDate)).toEqual(['2026-09-05', '2026-10-01'])
  })

  it('traduce el error a un mensaje de la app y no relanza, sin vaciar los logs ya cargados', async () => {
    mockFetch(listResponse([makeLog({ logDate: '2026-10-03' })]))
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

    // El `message` de la API no se muestra nunca: se elige la frase por código.
    expect(store.error).toBe('Something went wrong on our end. Please try again.')
    expect(store.error).not.toContain('Algo')
    expect(store.loading).toBe(false)
    expect(store.logs).toHaveLength(1)
  })

  it('mergeLog inserta un registro nuevo conservando el orden ascendente', async () => {
    mockFetch(
      listResponse([makeLog({ logDate: '2026-10-01' }), makeLog({ logDate: '2026-10-03' })]),
    )
    const store = useLogsStore()
    await store.fetch('week')

    store.mergeLog(makeLog({ logDate: '2026-10-02' }))

    expect(store.logs.map((log) => log.logDate)).toEqual(['2026-10-01', '2026-10-02', '2026-10-03'])
  })

  it('mergeLog reemplaza el registro del mismo logDate sin duplicar', async () => {
    mockFetch(
      listResponse([makeLog({ logDate: '2026-10-01' }), makeLog({ logDate: '2026-10-02' })]),
    )
    const store = useLogsStore()
    await store.fetch('week')

    const edited = { ...makeLog({ logDate: '2026-10-02' }), moodRating: 5 }
    store.mergeLog(edited)

    expect(store.logs).toHaveLength(2)
    expect(store.logs.find((log) => log.logDate === '2026-10-02')).toEqual(edited)
  })

  it('mergeLog funciona sin una carga previa', () => {
    const store = useLogsStore()

    store.mergeLog(makeLog({ logDate: '2026-10-02' }))
    store.mergeLog(makeLog({ logDate: '2026-10-01' }))

    expect(store.logs.map((log) => log.logDate)).toEqual(['2026-10-01', '2026-10-02'])
  })

  describe('con varias peticiones en vuelo', () => {
    /** Un `fetch` cuyas respuestas se resuelven a mano, en el orden que el test quiera. */
    function deferredFetch() {
      const resolvers: Array<(response: Response) => void> = []
      const rejecters: Array<(reason: unknown) => void> = []
      const signals: Array<AbortSignal | null | undefined> = []
      const spy = vi.fn(
        (_url: unknown, init?: RequestInit) =>
          new Promise<Response>((resolve, reject) => {
            resolvers.push(resolve)
            rejecters.push(reject)
            signals.push(init?.signal)
            // Como el fetch real: abortar rechaza con un AbortError.
            init?.signal?.addEventListener('abort', () =>
              reject(new DOMException('The operation was aborted.', 'AbortError')),
            )
          }),
      )
      vi.stubGlobal('fetch', spy)
      return {
        signals,
        resolve: (i: number, r: Response) => resolvers[i](r),
        reject: (i: number, e: unknown) => rejecters[i](e),
      }
    }

    it('una respuesta lenta de week no sobrescribe la de month pedida después', async () => {
      const http = deferredFetch()
      const store = useLogsStore()

      const week = store.fetch('week')
      const month = store.fetch('month')
      http.resolve(1, listResponse([makeLog({ logDate: '2026-09-05' })]))
      await month
      http.resolve(0, listResponse([makeLog({ logDate: '2026-10-01' })]))
      await week

      expect(store.logs.map((log) => log.logDate)).toEqual(['2026-09-05'])
      expect(store.loading).toBe(false)
    })

    it('mientras la última sigue en vuelo, la respuesta obsoleta no apaga loading ni escribe', async () => {
      const http = deferredFetch()
      const store = useLogsStore()

      const week = store.fetch('week')
      const month = store.fetch('month')
      http.resolve(0, listResponse([makeLog({ logDate: '2026-10-01' })]))
      await week

      expect(store.loading).toBe(true)
      expect(store.logs).toEqual([])

      http.resolve(1, listResponse([makeLog({ logDate: '2026-09-05' })]))
      await month
      expect(store.loading).toBe(false)
      expect(store.logs.map((log) => log.logDate)).toEqual(['2026-09-05'])
    })

    it('el fallo de una petición obsoleta no pisa la última ni deja error', async () => {
      const http = deferredFetch()
      const store = useLogsStore()

      const week = store.fetch('week')
      const month = store.fetch('month')
      http.resolve(1, listResponse([makeLog({ logDate: '2026-09-05' })]))
      await month
      http.reject(0, new TypeError('Failed to fetch'))
      await week

      expect(store.error).toBeNull()
      expect(store.logs).toHaveLength(1)
    })

    it('una petición nueva aborta la anterior y le pasa su propio signal', async () => {
      const http = deferredFetch()
      const store = useLogsStore()

      const week = store.fetch('week')
      const month = store.fetch('month')

      expect(http.signals[0]?.aborted).toBe(true)
      expect(http.signals[1]?.aborted).toBe(false)
      expect(http.signals[0]).not.toBe(http.signals[1])

      http.resolve(1, listResponse([makeLog({ logDate: '2026-09-05' })]))
      await Promise.all([week, month])
    })

    it('el AbortError propio no se muestra como error ni apaga loading', async () => {
      const http = deferredFetch()
      const store = useLogsStore()

      const week = store.fetch('week')
      const month = store.fetch('month')
      // `week` ya rechazó con AbortError al abortarse; `month` sigue en vuelo.
      await week

      expect(store.error).toBeNull()
      expect(store.loading).toBe(true)

      http.resolve(1, listResponse([makeLog({ logDate: '2026-09-05' })]))
      await month
      expect(store.error).toBeNull()
      expect(store.loading).toBe(false)
    })

    it('una petición que termina no se aborta al lanzar la siguiente', async () => {
      const http = deferredFetch()
      const store = useLogsStore()

      const first = store.fetch('week')
      http.resolve(0, listResponse([]))
      await first
      const second = store.fetch('month')

      expect(http.signals[0]?.aborted).toBe(false)
      http.resolve(1, listResponse([]))
      await second
    })

    it('un mergeLog que llega con un fetch en vuelo sobrevive a la respuesta', async () => {
      const http = deferredFetch()
      const store = useLogsStore()

      const pending = store.fetch('week')
      store.mergeLog(makeLog({ logDate: '2026-10-02', moodRating: 5 }))
      // La respuesta se calculó antes del guardado: trae el día viejo y no el nuevo.
      http.resolve(
        0,
        listResponse([
          makeLog({ logDate: '2026-10-01' }),
          makeLog({ logDate: '2026-10-02', moodRating: 1 }),
        ]),
      )
      await pending

      expect(store.logs.map((log) => log.logDate)).toEqual(['2026-10-01', '2026-10-02'])
      expect(store.logs[1].moodRating).toBe(5)
    })

    it('un mergeLog durante un fetch obsoleto también se conserva en el último', async () => {
      const http = deferredFetch()
      const store = useLogsStore()

      const week = store.fetch('week')
      store.mergeLog(makeLog({ logDate: '2026-10-02' }))
      const month = store.fetch('month')
      http.resolve(0, listResponse([]))
      await week
      http.resolve(1, listResponse([makeLog({ logDate: '2026-09-05' })]))
      await month

      expect(store.logs.map((log) => log.logDate)).toEqual(['2026-09-05', '2026-10-02'])
    })

    it('un mergeLog sin fetch en vuelo no se reaplica en cargas posteriores', async () => {
      const store = useLogsStore()
      store.mergeLog(makeLog({ logDate: '2026-10-02' }))

      mockFetch(listResponse([makeLog({ logDate: '2026-10-01' })]))
      await store.fetch('week')

      expect(store.logs.map((log) => log.logDate)).toEqual(['2026-10-01'])
    })
  })
})
