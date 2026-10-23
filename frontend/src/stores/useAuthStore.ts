/**
 * Sesión del usuario.
 *
 * Guarda quién está dentro y si hay una consulta en vuelo. No decide nada de
 * navegación: un guard de router decide a dónde va cada quien según lo que
 * devuelva `fetchMe`.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'

import { ApiError } from '../services/api-client'
import { logout as logoutRequest, me } from '../services/auth.api'
import type { User } from '../types/daily-log'

export const useAuthStore = defineStore('auth', () => {
  /** El usuario de la sesión, o `null` si no hay ninguna. */
  const user = ref<User | null>(null)
  /** `true` mientras `fetchMe` está en vuelo, para que la vista no parpadee. */
  const loading = ref(false)
  /** La consulta en vuelo, compartida por quien llame a `fetchMe` mientras dure. */
  let inFlight: Promise<User | null> | null = null

  /**
   * Consulta la sesión al backend y la deja en el store.
   *
   * Un 401 NO es un fallo que haya que reportar: significa que no hay cookie o
   * que caducó, que es el estado mayoritario de un visitante anónimo. Se
   * traduce a `user = null` y se devuelve `null`, con lo que el 401 no llega a
   * ninguna UI como error. Cualquier otro error se propaga: si la API está
   * caída, eso sí es un problema que alguien tiene que ver, y tragárselo aquí
   * dejaría una pantalla vacía sin explicación.
   *
   * Se decide por el status y no por el `code`: un 401 que no venga de la API
   * (un proxy, por ejemplo) llega sin sobre y `apiRequest` lo degrada a
   * `INTERNAL_ERROR`.
   *
   * Las llamadas concurrentes (el guard y la vista, por ejemplo) comparten la
   * misma promesa: una sola petición a /api/auth/me, con el mismo resultado y,
   * si falla, el mismo error. Al terminar se libera, así que la siguiente llamada
   * vuelve a consultar al backend.
   *
   * @returns El usuario, o `null` si no hay sesión.
   * @throws {ApiError} Cualquier estado que no sea 401.
   */
  function fetchMe(): Promise<User | null> {
    inFlight ??= requestMe().finally(() => {
      inFlight = null
    })
    return inFlight
  }

  async function requestMe(): Promise<User | null> {
    loading.value = true
    try {
      user.value = await me()
      return user.value
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        user.value = null
        return null
      }
      throw error
    } finally {
      loading.value = false
    }
  }

  /**
   * Cierra la sesión y vacía el store.
   *
   * `user` se limpia solo si el backend confirma el borrado de la cookie: si la
   * petición falla, la sesión sigue viva en el servidor y dejarla puesta sería
   * mentir. El error sube para que quien llama decida qué mostrar.
   */
  async function logout(): Promise<void> {
    await logoutRequest()
    user.value = null
  }

  return { user, loading, fetchMe, logout }
})
