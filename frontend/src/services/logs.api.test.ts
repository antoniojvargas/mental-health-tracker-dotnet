import { afterEach, describe, expect, it, vi } from 'vitest'

import { today } from './logs.api'

function respond(status: number, body: unknown = {}) {
  return vi.fn().mockResolvedValue(
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
    expect(String(fetchMock.mock.calls[0][0])).toBe('/api/logs/today?date=2026-03-15')
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
