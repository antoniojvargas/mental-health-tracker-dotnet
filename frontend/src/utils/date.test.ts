import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest'

import { parseIsoDate, todayIso, toIsoDate } from './date'

// Se cambia `process.env.TZ` en caliente con `vi.stubEnv`: Node recalcula la zona al
// asignarla. Una positiva (Tokyo, UTC+9) y otra negativa (Bogotá, UTC-5) son las que
// separan el día local del día UTC; con una sola, un `toISOString()` accidental pasaría
// el test.
describe.each(['Asia/Tokyo', 'America/Bogota'])('utils/date en %s', (zone) => {
  beforeAll(() => {
    vi.stubEnv('TZ', zone)
  })

  afterAll(() => {
    vi.unstubAllEnvs()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('toIsoDate usa el día local, no el UTC', () => {
    // 20:00 locales del 15 de marzo: en Bogotá ya es el 16 en UTC (01:00Z); en Tokyo,
    // 20:00 del 15 equivale al 15 a las 11:00Z.
    expect(toIsoDate(new Date(2026, 2, 15, 20, 0, 0))).toBe('2026-03-15')
    // 00:30 locales del 15: en Tokyo todavía es el 14 en UTC (15:30Z del día 14).
    expect(toIsoDate(new Date(2026, 2, 15, 0, 30, 0))).toBe('2026-03-15')
  })

  it('toIsoDate rellena con ceros mes y día', () => {
    expect(toIsoDate(new Date(2026, 0, 5, 12))).toBe('2026-01-05')
    expect(toIsoDate(new Date(2026, 11, 31, 23, 59))).toBe('2026-12-31')
  })

  it('todayIso devuelve el día local del reloj', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 2, 15, 20, 0, 0))

    expect(todayIso()).toBe('2026-03-15')
  })

  it('parseIsoDate devuelve la medianoche local de ese día', () => {
    const date = parseIsoDate('2026-03-15')

    expect(date.getFullYear()).toBe(2026)
    expect(date.getMonth()).toBe(2)
    expect(date.getDate()).toBe(15)
    expect(date.getHours()).toBe(0)
  })

  it('parseIsoDate y toIsoDate son inversas', () => {
    for (const iso of ['2026-01-01', '2026-02-28', '2028-02-29', '2026-12-31']) {
      expect(toIsoDate(parseIsoDate(iso))).toBe(iso)
    }
  })
})

describe('el desfase que evita el módulo', () => {
  beforeAll(() => {
    vi.stubEnv('TZ', 'America/Bogota')
  })

  afterAll(() => {
    vi.unstubAllEnvs()
  })

  it('new Date(iso) y toISOString() mueven el día en Bogotá; el módulo no', () => {
    // 20:00 en Bogotá del 15 es el 16 en UTC.
    const evening = new Date(2026, 2, 15, 20, 0, 0)
    expect(evening.toISOString().slice(0, 10)).toBe('2026-03-16')
    expect(toIsoDate(evening)).toBe('2026-03-15')

    // new Date('2026-03-15') es medianoche UTC, es decir, el 14 a las 19:00 locales.
    expect(new Date('2026-03-15').getDate()).toBe(14)
    expect(parseIsoDate('2026-03-15').getDate()).toBe(15)
  })
})
