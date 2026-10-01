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
   * Ojo con leer el `code` para decidir esto: el 401 de `RequireAuthMiddleware`
   * trae `code: 'UNAUTHORIZED'`, pero el de `AuthController.Me` —cookie válida
   * con usuario borrado— llega sin cuerpo y `apiRequest` lo degrada a
   * `INTERNAL_ERROR`. Por eso se mira el status, no el code.
   *
   * @returns El usuario, o `null` si no hay sesión.
   * @throws {ApiError} Cualquier estado que no sea 401.
   */
  async function fetchMe(): Promise<User | null> {
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
