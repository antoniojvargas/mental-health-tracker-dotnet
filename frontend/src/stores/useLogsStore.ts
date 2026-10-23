/**
 * Registros diarios cargados para el panel.
 *
 * La lista se mantiene siempre ordenada por `logDate` ascendente para que las
 * vistas (gráficos de tendencias) la consuman tal cual, sin reordenar. `fetch`
 * pide una ventana fija al backend y `mergeLog` integra un registro recién
 * guardado sin volver a pedir toda la lista.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'

import { describeError } from '../services/error-messages'
import { list } from '../services/logs.api'
import type { DailyLog } from '../types/daily-log'
import { toIsoDate } from '../utils/date'

/** Ventanas admitidas: los últimos 7 o 30 días, hoy incluido. */
export type LogRange = 'week' | 'month'

/** Días que abarca cada ventana, contando hoy. */
const RANGE_DAYS: Record<LogRange, number> = { week: 7, month: 30 }

export const useLogsStore = defineStore('logs', () => {
  /** Los registros cargados, siempre por `logDate` ascendente. */
  const logs = ref<DailyLog[]>([])
  /** `true` mientras `fetch` está en vuelo, para que la vista no parpadee. */
  const loading = ref(false)
  /**
   * Mensaje del último fallo de `fetch`, o `null` si la última carga fue bien.
   *
   * Es texto para pantalla, ya traducido por `describeError`: nunca el `message`
   * crudo de la API, que puede traer nombres de campos o detalles del servidor.
   */
  const error = ref<string | null>(null)

  /** Identifica la última petición lanzada; una respuesta con otro número es obsoleta. */
  let latestRequest = 0
  /**
   * Registros integrados con `mergeLog` mientras hay un `fetch` en vuelo. La
   * respuesta puede haberse calculado antes de ese guardado, así que al llegar se
   * vuelven a aplicar encima para no perderlos.
   */
  let mergedWhileFetching: DailyLog[] = []

  /**
   * Carga la ventana pedida (hoy incluido) y reemplaza la lista.
   *
   * El rango se calcula en hora local —`from` es hoy menos los días de la
   * ventana y `to` es hoy— y se ordena lo que devuelva el servidor: el backend
   * no garantiza el orden y las vistas de tendencias lo dan por hecho.
   *
   * Los fallos se dejan en `error` en vez de relanzarse para que la vista los
   * pinte sin envolver la llamada en un try/catch. Un fallo nunca vacía `logs`:
   * una recarga que falla no borra lo que ya había en pantalla.
   *
   * Gana la última petición: si una respuesta lenta llega después de otra pedida
   * más tarde (p. ej. `week` tras `month`), se descarta, también su error y su
   * fin de `loading`. Los `mergeLog` que ocurrieron mientras estaba en vuelo se
   * reaplican sobre la respuesta.
   */
  async function fetch(range: LogRange): Promise<void> {
    const request = ++latestRequest
    loading.value = true
    error.value = null
    try {
      const to = new Date()
      const from = new Date(to)
      from.setDate(from.getDate() - (RANGE_DAYS[range] - 1))
      const { data } = await list(toIsoDate(from), toIsoDate(to))
      if (request !== latestRequest) return
      logs.value = [...data].sort((a, b) => a.logDate.localeCompare(b.logDate))
      // Se vacía antes de reaplicar: `mergeLog` vuelve a anotar mientras `loading` es true.
      const merged = mergedWhileFetching
      mergedWhileFetching = []
      for (const log of merged) mergeLog(log)
    } catch (thrown) {
      if (request !== latestRequest) return
      // Se guarda un mensaje ya traducido, nunca `thrown.message`: el `message`
      // de la API puede traer nombres de campo o detalles internos, y este string
      // termina tal cual en la pantalla.
      error.value = describeError(thrown)
    } finally {
      if (request === latestRequest) {
        loading.value = false
        mergedWhileFetching = []
      }
    }
  }

  /**
   * Inserta o reemplaza un registro por `logDate`, conservando el orden
   * ascendente. Es el enganche para reflejar un guardado sin recargar.
   *
   * Se identifica por `logDate` y no por `id` porque el upsert del backend crea
   * o actualiza sobre esa fecha: dos registros del mismo día son el mismo.
   */
  function mergeLog(log: DailyLog): void {
    if (loading.value) mergedWhileFetching.push(log)
    const index = logs.value.findIndex((existing) => existing.logDate === log.logDate)
    if (index === -1) {
      logs.value.push(log)
    } else {
      logs.value[index] = log
    }
    logs.value.sort((a, b) => a.logDate.localeCompare(b.logDate))
  }

  return { logs, loading, error, fetch, mergeLog }
})
