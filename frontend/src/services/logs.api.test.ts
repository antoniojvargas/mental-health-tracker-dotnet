import { afterEach, describe, expect, it, vi } from 'vitest'

import { create, list, today } from './logs.api'

function respond(status: number, body: unknown = {}) {
  return vi.fn<typeof fetch>().mockResolvedValue(
    new Response(JSON.stringify(body), {
      status,
      headers: { 'Content-Type': 'application/json' },
    }),
  )
}

describe('logs.api · today', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('envía el día del cliente como ?date=', async () => {
    const fetchMock = respond(200, { id: '1', logDate: '2026-03-15' })
    vi.stubGlobal('fetch', fetchMock)

    const log = await today('2026-03-15')

    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(fetchMock.mock.calls[0][0]).toBe('/api/logs/today?date=2026-03-15')
    expect(log?.logDate).toBe('2026-03-15')
  })

  it('traduce el 404 a null', async () => {
    vi.stubGlobal('fetch', respond(404))

    await expect(today('2026-03-15')).resolves.toBeNull()
  })

  it('traduce a null el 404 con el sobre NOT_FOUND que manda el backend', async () => {
    vi.stubGlobal(
      'fetch',
      respond(404, { error: { code: 'NOT_FOUND', message: 'No log for that day', details: [] } }),
    )

    await expect(today('2026-03-15')).resolves.toBeNull()
  })

  it('propaga el resto de errores', async () => {
    vi.stubGlobal(
      'fetch',
      respond(400, { error: { code: 'VALIDATION_ERROR', message: 'x', details: [] } }),
    )

    await expect(today('2030-01-01')).rejects.toMatchObject({ status: 400 })
  })
})

describe('logs.api · list', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('arma from y to en la query y devuelve la respuesta tal cual', async () => {
    const body = {
      data: [{ id: '1', logDate: '2026-03-10' }],
      meta: { from: '2026-03-01', to: '2026-03-31', limit: 100, offset: 0, total: 1 },
    }
    const fetchMock = respond(200, body)
    vi.stubGlobal('fetch', fetchMock)

    const result = await list('2026-03-01', '2026-03-31')

    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(fetchMock.mock.calls[0][0]).toBe('/api/logs?from=2026-03-01&to=2026-03-31')
    expect(fetchMock.mock.calls[0][1]?.method).toBe('GET')
    expect(result).toEqual(body)
  })

  it('pasa el signal a fetch para poder cancelar la petición', async () => {
    const fetchMock = respond(200, { data: [], meta: {} })
    vi.stubGlobal('fetch', fetchMock)
    const controller = new AbortController()

    await list('2026-03-01', '2026-03-31', { signal: controller.signal })

    expect(fetchMock.mock.calls[0][1]?.signal).toBe(controller.signal)
  })

  it('sin opciones no manda signal', async () => {
    const fetchMock = respond(200, { data: [], meta: {} })
    vi.stubGlobal('fetch', fetchMock)

    await list('2026-03-01', '2026-03-31')

    expect(fetchMock.mock.calls[0][1]?.signal).toBeUndefined()
  })

  it('propaga el error de validación del rango', async () => {
    vi.stubGlobal(
      'fetch',
      respond(400, { error: { code: 'VALIDATION_ERROR', message: 'x', details: [] } }),
    )

    await expect(list('2026-03-31', '2026-03-01')).rejects.toMatchObject({
      status: 400,
      code: 'VALIDATION_ERROR',
    })
  })
})

describe('logs.api · today · propagación', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it.each([
    [401, 'UNAUTHORIZED'],
    [429, 'RATE_LIMITED'],
    [500, 'INTERNAL_ERROR'],
  ])('propaga el %i como ApiError', async (status, code) => {
    vi.stubGlobal('fetch', respond(status, { error: { code, message: 'x', details: [] } }))

    await expect(today('2026-03-15')).rejects.toMatchObject({ status, code })
  })

  it('propaga un fallo de red', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

    await expect(today('2026-03-15')).rejects.toBeInstanceOf(TypeError)
  })
})

describe('logs.api · create', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('hace POST /api/logs con el cuerpo en JSON y devuelve el registro', async () => {
    const saved = { id: '1', logDate: '2026-03-15' }
    const fetchMock = respond(201, saved)
    vi.stubGlobal('fetch', fetchMock)

    const input = { logDate: '2026-03-15', moodRating: 3 } as Parameters<typeof create>[0]
    const result = await create(input)

    expect(fetchMock.mock.calls[0][0]).toBe('/api/logs')
    expect(fetchMock.mock.calls[0][1]?.method).toBe('POST')
    expect(JSON.parse(fetchMock.mock.calls[0][1]?.body as string)).toEqual(input)
    expect(result).toEqual(saved)
  })
})
