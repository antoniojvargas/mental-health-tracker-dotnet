/**
 * Fechas calendario del cliente, siempre en hora local.
 *
 * Es la única fuente del "hoy" del frontend: la API habla en `yyyy-MM-dd` y el día
 * lo decide el usuario en su zona, no el servidor en UTC.
 */

import type { IsoDate } from '../types/daily-log'

/**
 * "yyyy-MM-dd" en hora local.
 *
 * No sirve `toISOString()`: pasa a UTC y en un huso positivo devolvería el día
 * anterior por la mañana (y el siguiente por la noche en uno negativo).
 */
export function toIsoDate(date: Date): IsoDate {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

/** El día de hoy en hora local. */
export const todayIso = (): IsoDate => toIsoDate(new Date())

/**
 * "yyyy-MM-dd" a `Date` local a medianoche.
 *
 * No sirve `new Date(iso)`: lee ese formato como UTC y en un huso negativo
 * mostraría el día anterior.
 */
export function parseIsoDate(iso: IsoDate): Date {
  const [year, month, day] = iso.split('-').map(Number)
  return new Date(year, month - 1, day)
}
