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

import { ApiError } from '../services/api-client'
import { list } from '../services/logs.api'
import type { DailyLog, IsoDate } from '../types/daily-log'

/** Ventanas admitidas: los últimos 7 o 30 días, hoy incluido. */
export type LogRange = 'week' | 'month'

/** Días que abarca cada ventana, contando hoy. */
const RANGE_DAYS: Record<LogRange, number> = { week: 7, month: 30 }

/**
 * "yyyy-MM-dd" en hora local, que es justo lo que espera la API.
 *
 * No sirve `toISOString()`: pasa a UTC y en un huso positivo devolvería el día
 * anterior por la noche, como avisa el JSDoc de `logs.api`.
 */
function toIsoDate(date: Date): IsoDate {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

export const useLogsStore = defineStore('logs', () => {
  /** Los registros cargados, siempre por `logDate` ascendente. */
  const logs = ref<DailyLog[]>([])
  /** `true` mientras `fetch` está en vuelo, para que la vista no parpadee. */
  const loading = ref(false)
  /** Mensaje del último fallo de `fetch`, o `null` si la última carga fue bien. */
  const error = ref<string | null>(null)

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
   */
  async function fetch(range: LogRange): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const to = new Date()
      const from = new Date()
      from.setDate(from.getDate() - (RANGE_DAYS[range] - 1))
      const { data } = await list(toIsoDate(from), toIsoDate(to))
      logs.value = [...data].sort((a, b) => a.logDate.localeCompare(b.logDate))
    } catch (thrown) {
      error.value =
        thrown instanceof ApiError ? thrown.message : 'No se pudieron cargar los registros'
    } finally {
      loading.value = false
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
