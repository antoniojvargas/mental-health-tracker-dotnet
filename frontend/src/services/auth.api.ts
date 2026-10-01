/**
 * Endpoints de sesión, sobre /api/auth.
 *
 * Solo GET /api/auth/me y POST /api/auth/logout. El login no vive aquí: la
 * entrada es el callback de Google (GET /api/auth/google/callback), que responde
 * con un 302 al frontend y por tanto lo maneja el navegador, no `fetch`.
 */

import { apiRequest } from './api-client'
import type { User } from '../types/daily-log'

/**
 * El usuario de la sesión actual.
 *
 * @throws {ApiError} 401 si no hay cookie `access_token` válida o si el usuario
 * ya no existe. Ojo: ese 401 llega por dos caminos y solo uno de los dos trae
 * sobre de error. `RequireAuthMiddleware` lanza `UnauthorizedException`, que el
 * manejador global convierte en el sobre completo con `code: 'UNAUTHORIZED'`,
 * pero `AuthController.Me` responde con un `Unauthorized()` a secas cuando la
 * cookie sí es válida y lo que no existe es el usuario, y ese cuerpo va vacío.
 * Quien llame tiene que fiarse del status, no de poder leer `error.code`.
 */
export function me(): Promise<User> {
  return apiRequest<User>('/auth/me')
}

/**
 * Cierra la sesión borrando la cookie `access_token`.
 *
 * @returns `undefined`: el endpoint responde 204.
 * @throws {ApiError} `RATE_LIMITED` si se supera la cuota de la política `auth`.
 */
export function logout(): Promise<void> {
  return apiRequest<void>('/auth/logout', { method: 'POST' })
}
