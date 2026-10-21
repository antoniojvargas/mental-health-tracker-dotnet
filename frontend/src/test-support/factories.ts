/**
 * Fábricas de datos de prueba del contrato (`DailyLog`, `User`).
 *
 * Cada una devuelve un valor completo y válido, con valores neutros, y deja sobrescribir solo lo
 * que el test quiere variar: así un caso declara lo que importa y no un registro entero. Viven en
 * `src/` por la misma razón que `http.ts`: las typechequea `vue-tsc` como al resto del código.
 */

import type { DailyLog, User } from '../types/daily-log'

/** Un registro diario válido y neutro; `id` sale de `logDate` salvo que se indique. */
export function makeLog(overrides: Partial<DailyLog> = {}): DailyLog {
  const logDate = overrides.logDate ?? '2026-08-01'

  return {
    id: `id-${logDate}`,
    logDate,
    moodRating: 3,
    anxietyLevel: 5,
    stressLevel: 5,
    sleepHours: 7,
    sleepQuality: 4,
    sleepDisturbances: [],
    activityType: 'none',
    activityMinutes: null,
    socialFrequency: 'none',
    symptoms: [],
    notes: null,
    createdAt: '2026-09-13T17:18:21.1234567+00:00',
    updatedAt: '2026-09-13T17:18:21.1234567+00:00',
    ...overrides,
  }
}

/** Un usuario con sesión. */
export function makeUser(overrides: Partial<User> = {}): User {
  return {
    id: 'user-1',
    email: 'ana@example.com',
    name: 'Ana Gómez',
    avatarUrl: null,
    ...overrides,
  }
}
