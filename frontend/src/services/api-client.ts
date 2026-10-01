/**
 * Cliente HTTP de la API.
 *
 * Envuelve `fetch` para que ninguna otra parte del frontend tenga que acordarse de
 * las tres reglas que la API da por descontadas:
 *
 *   1. La ruta va con el prefijo `/api` y es relativa al origen, nunca absoluta.
 *      El proxy de Vite (ver vite.config.ts) y el de producción resuelven `/api`
 *      contra el backend, así que la SPA no necesita saber dónde vive la API.
 *   2. La sesión viaja en la cookie httpOnly `access_token`, no en una cabecera
 *      `Authorization`. Por eso `credentials: 'include'` es obligatorio en TODAS
 *      las peticiones, también en las de escritura: sin él el navegador no manda
 *      la cookie cross-site y `RequireAuthMiddleware` responde 401.
 *   3. Los 4xx y 5xx no se devuelven: se lanzan como `ApiError`, con el `code` y
 *      el `message` de `ErrorBody` ya desenrollados del sobre `error`.
 *
 * Los tipos del contrato viven en src/types/daily-log.ts, no aquí.
 */

import type {
  ApiErrorBody,
  ApiErrorCode,
  ApiErrorDetail,
  ApiErrorResponse,
} from '../types/daily-log'

/** Prefijo común a todas las rutas. La API enruta bajo `api/` (AuthController). */
const API_PREFIX = '/api'

/**
 * Fallo de una respuesta de la API.
 *
 * Se lanza en cuanto `response.ok` es false, así que quien llama recibe el status
 * HTTP y el código de dominio ya consultingos. `details` solo viene poblado en
 * `VALIDATION_ERROR`, con una entrada por campo; `field` llega en PascalCase
 * ("MoodRating", "Symptoms[0].Severity"), ver el tipo.
 */
export class ApiError extends Error {
  readonly status: number
  readonly code: ApiErrorCode
  readonly details: ApiErrorDetail[]

  constructor(status: number, body: ApiErrorBody) {
    super(body.message)
    this.name = 'ApiError'
    this.status = status
    this.code = body.code
    this.details = body.details
  }
}

export interface ApiRequestInit {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  /**
   * Se serializa con `JSON.stringify`. Omitirlo (o pasar `undefined`) envía la
   * petición sin cuerpo y sin `Content-Type`.
   */
  body?: unknown
  signal?: AbortSignal
}

/**
 * Hace la petición y devuelve el cuerpo ya parseado.
 *
 * `path` va SIN el prefijo: `apiRequest('/auth/me')` pega contra `/api/auth/me`.
 * Los query params van en el propio `path` (`/logs?limit=20&offset=0`); no hay
 * helper que los construya porque hoy cada listado los arma con sus propios
 * defaults y conviene que se vean en la llamada.
 *
 * @returns El cuerpo parseado, o `undefined` si la respuesta no tiene cuerpo.
 * @throws {ApiError} Si la respuesta no es 2xx.
 */
export async function apiRequest<T>(path: string, init: ApiRequestInit = {}): Promise<T> {
  const { method = 'GET', body, signal } = init

  const headers: Record<string, string> = {}
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }

  const response = await fetch(`${API_PREFIX}${path}`, {
    method,
    headers,
    credentials: 'include',
    body: body === undefined ? undefined : JSON.stringify(body),
    signal,
  })

  if (!response.ok) {
    throw new ApiError(response.status, await readErrorBody(response))
  }

  return parseBody<T>(response)
}

/**
 * Un 204 no tiene cuerpo, pero `response.json()` lo rechaza igual con un
 * `SyntaxError`, así que hay que cortarlo antes de intentar parsear. El texto
 * vacío se cubre aparte porque un 200 con cuerpo `{}` o una respuesta vacía de
 * un 205 también deben devolver `undefined` y no reventar.
 */
async function parseBody<T>(response: Response): Promise<T> {
  if (response.status === 204) {
    return undefined as T
  }

  const text = await response.text()
  return text === '' ? (undefined as T) : (JSON.parse(text) as T)
}

/**
 * Desenvuelve `ErrorResponse` (el `error` anidado) y degrada a un cuerpo válido
 * si la respuesta no lo trae.
 *
 * La degradación no es paranoia: `AuthController.Me` responde con un `Unauthorized()`
 * desnudo —sin sobre `error`— cuando el token de la cookie no resuelve a un
 * usuario, y por delante de la API puede haber un proxy que conteste con HTML.
 * Un `JSON.parse` a pelo tiraría el `TypeError` y perdería el status, que es justo
 * lo que la UI necesita para decidir a dónde va el usuario.
 */
async function readErrorBody(response: Response): Promise<ApiErrorBody> {
  const fallback: ApiErrorBody = {
    // `INTERNAL_ERROR` es el código que ya emite el manejador global para
    // cualquier AppException sin código propio, así que no inventa un valor.
    code: 'INTERNAL_ERROR',
    message: response.statusText || `HTTP ${response.status}`,
    details: [],
  }

  let text: string
  try {
    text = await response.text()
  } catch {
    return fallback
  }

  if (text === '') {
    return fallback
  }

  try {
    const parsed: unknown = JSON.parse(text)
    if (!isErrorResponse(parsed)) {
      return fallback
    }

    // `ErrorBody` siempre serializa `details`, pero normalizarlo aquí evita que un
    // cuerpo sin ese campo deje `details` en `undefined` y reviente el `.map()` de
    // quien lo recorra.
    return { ...parsed.error, details: parsed.error.details ?? [] }
  } catch {
    return fallback
  }
}

/**
 * Valida solo la FORMA del sobre, no el valor de `code`.
 *
 * Comprobar que el código es uno de los cinco de `ApiErrorCode` obligaría a
 * duplicar esa lista aquí, que es justo lo que daily-log.ts evita al ser el
 * punto único del contrato. Si el backend añadiera un código, este cliente lo
 * dejaría pasar sin quebrarse y la deriva se seguiría detectando regenerando el
 * OpenAPI, que es el mecanismo acordado para eso.
 */
function isErrorResponse(value: unknown): value is ApiErrorResponse {
  if (typeof value !== 'object' || value === null) {
    return false
  }

  const error = (value as { error?: unknown }).error
  if (typeof error !== 'object' || error === null) {
    return false
  }

  const { code, message } = error as Partial<ApiErrorBody>
  return typeof code === 'string' && typeof message === 'string'
}
