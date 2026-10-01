/**
 * Utilidades compartidas por los tests.
 *
 * Vive en `src/` a propósito: `tsconfig.app.json` incluye todo lo que hay bajo
 * ese directorio, así que esto lo typechequea `vue-tsc` igual que el código de
 * la app y un test que no compila no se cuela. No lo importa nada del bundle,
 * porque solo lo usan los ficheros de test.
 */

import { expect, vi } from 'vitest'

import { ApiError } from '../services/api-client'

/**
 * Sustituye el `fetch` global por un stub que resuelve la respuesta dada y lo
 * devuelve como spy, para poder inspeccionar cómo se llamó.
 *
 * El `Response` es real, no un doble: la lógica que decide qué hacer con el
 * cuerpo (`.text()`, `status`, `statusText`, `ok`) es parte de lo que se está
 * probando, y un objeto plano fingiendo ser `Response` la dejaría fuera del
 * alcance del test. Node 22 trae `Response` en el global, así que esto no
 * necesita `undici` ni MSW.
 */
export function mockFetch(response: Response) {
  const fetchSpy = vi.fn().mockResolvedValue(response)
  vi.stubGlobal('fetch', fetchSpy)
  return fetchSpy
}

/** Un `Response` JSON con el status indicado. */
export function jsonResponse(body: unknown, status = 200, statusText?: string): Response {
  return new Response(JSON.stringify(body), {
    status,
    statusText,
    headers: { 'Content-Type': 'application/json' },
  })
}

/** Un `Response` sin cuerpo, para los 204 de logout. */
export function emptyResponse(status: number, statusText?: string): Response {
  return new Response(null, { status, statusText })
}

/**
 * Espera el rechazo y devuelve el `ApiError` ya tipado.
 *
 * Mejor que `.catch((thrown) => thrown)`: así el resultado es `ApiError` y no
 * `unknown`, y una promesa que se resuelve en vez de rechazar revienta aquí con
 * un mensaje claro en lugar de colarse en las aserciones siguientes.
 */
export async function captureError(promise: Promise<unknown>): Promise<ApiError> {
  try {
    await promise
  } catch (thrown) {
    expect(thrown).toBeInstanceOf(ApiError)
    return thrown as ApiError
  }

  throw new Error('Se esperaba que la petición fuera rechazada, pero se resolvió')
}
