/**
 * Endpoints de registro diario, sobre GET/POST /api/logs.
 *
 * Es la única capa que conoce las rutas y los query params: el resto de la app
 * llama a estas funciones y trabaja con los tipos de src/types/daily-log.ts, sin
 * ver nada de HTTP. La autenticación y el prefijo los pone `apiRequest`.
 */

import { apiRequest, ApiError } from './api-client'
import type {
  CreateDailyLogInput,
  DailyLog,
  DailyLogListResponse,
  IsoDate,
} from '../types/daily-log'

/**
 * Escribe el registro de un día (upsert).
 *
 * Ojo: no es un insert puro. POST es un upsert sobre (user_id, log_date) y
 * devuelve 201 al crear y 200 al actualizar, con la misma forma en los dos
 * casos, así que quien llama no puede distinguir uno de otro por la respuesta.
 * Si la UI necesita decirlo ("guardado" frente a "actualizado"), hay que
 * mirar si el log ya existía antes de llamar.
 *
 * La respuesta trae el registro ya guardado, con `id` y las marcas de tiempo, así
 * que no hace falta un GET de confirmación.
 *
 * @throws {ApiError} `VALIDATION_ERROR` con un `detail` por campo si el rango no
 * cuadra (mood 1-5, ansiedad y estrés 1-10, sueño 0-24, calidad 1-5, minutos
 * 0-600) o si `logDate` está en el futuro; `RATE_LIMITED` si se pasa de las 30
 * escrituras por 15 min y usuario.
 */
export function create(input: CreateDailyLogInput): Promise<DailyLog> {
  return apiRequest<DailyLog>('/logs', { method: 'POST', body: input })
}

/**
 * Registros de un rango cerrado, ambos días incluidos.
 *
 * `from` y `to` son "yyyy-MM-dd" y el backend exige `from <= to` con un rango
 * total de 366 días como mucho. Omitirlos no es una opción en esta firma: sin
 * ellos el servidor usa hoy-29 hasta hoy, un rango que el formulario de
 * tendencias no quiere imponer.
 *
 * Ojo con `meta.total` frente a `data.length`: el rango admite 366 días y la
 * respuesta trae como mucho 100 (el `limit` por defecto del servidor), así que
 * `data` puede venir truncada y `meta.total` es el que dice cuántas hay. Un
 * gráfico de un año con esta función saldría incompleto sin avisar.
 *
 * `options.signal` cancela la petición: la promesa rechaza con el `AbortError` del
 * navegador, no con un `ApiError`, así que quien aborta debe distinguirlo.
 *
 * @throws {ApiError} `VALIDATION_ERROR` si el rango no cumple las reglas de arriba.
 */
export function list(
  from: IsoDate,
  to: IsoDate,
  options: { signal?: AbortSignal } = {},
): Promise<DailyLogListResponse> {
  const query = new URLSearchParams({ from, to })
  return apiRequest<DailyLogListResponse>(`/logs?${query.toString()}`, { signal: options.signal })
}

/**
 * El registro de hoy, o `null` si el usuario aún no ha escrito ninguno.
 *
 * Devuelve `null` en vez de propagar el 404 porque "hoy no hay nada" es el
 * estado normal de la pantalla, no un fallo: es lo que decide si el formulario
 * se abre vacío o relleno. Además el 404 de este endpoint llega DESNUDO
 * (`NotFound()` a secas, sin el sobre `error`), así que si se dejara pasar
 * llegaría como `ApiError` con `code: 'INTERNAL_ERROR'`, que miente: no hubo
 * ningún error interno. Cualquier otro error se propaga sin tocar.
 *
 * El día lo decide el cliente: `date` es el "hoy" local del navegador (ver
 * `todayIso`) y viaja como `?date=`. Sin él, el servidor usaría su día UTC, que
 * en un huso distinto de UTC puede ser el de ayer o el de mañana y haría que el
 * formulario se rellenara con el registro de otro día.
 *
 * @throws {ApiError} `VALIDATION_ERROR` si `date` supera en más de un día la fecha UTC.
 */
export async function today(date: IsoDate): Promise<DailyLog | null> {
  const query = new URLSearchParams({ date })
  try {
    return await apiRequest<DailyLog>(`/logs/today?${query.toString()}`)
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      return null
    }
    throw error
  }
}
