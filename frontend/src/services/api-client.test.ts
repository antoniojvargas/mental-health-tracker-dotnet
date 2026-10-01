import { afterEach, describe, expect, it, vi } from 'vitest'

import { apiRequest, ApiError } from './api-client'

/**
 * Sustituye `fetch` global por un stub que resuelve la respuesta dada y lo
 * devuelve como spy, para poder inspeccionar cómo se llamó.
 *
 * El `Response` es real, no un doble: la lógica que decide qué hacer con el
 * cuerpo (`.text()`, `status`, `statusText`, `ok`) es parte de lo que se está
 * probando, y un objeto plano fingiendo ser `Response` la dejaría fuera del
 * alcance del test. Node 22 trae `Response` en el global, así que esto no
 * necesita `undici` ni MSW.
 */
function mockFetch(response: Response) {
  const fetchSpy = vi.fn().mockResolvedValue(response)
  vi.stubGlobal('fetch', fetchSpy)
  return fetchSpy
}

/** Los headers con los que se envió la petición, para poder comprobar el Content-Type. */
function headersOf(fetchSpy: ReturnType<typeof mockFetch>): HeadersInit {
  return fetchSpy.mock.calls[0][1].headers as HeadersInit
}

/**
 * Espera el rechazo y devuelve el `ApiError` ya tipado.
 *
 * Mejor que `.catch((thrown) => thrown)`: así el resultado es `ApiError` y no
 * `unknown`, y una promesa que se resuelve en vez de rechazar revienta aquí con
 * un mensaje claro en lugar de colarse en las aserciones siguientes.
 */
async function captureError(promise: Promise<unknown>): Promise<ApiError> {
  try {
    await promise
  } catch (thrown) {
    expect(thrown).toBeInstanceOf(ApiError)
    return thrown as ApiError
  }

  throw new Error('Se esperaba que la petición fuera rechazada, pero se resolvió')
}

describe('apiRequest', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('devuelve el cuerpo parseado y envía la cookie de sesión', async () => {
    const user = { id: 'a1', email: 'a@b.c', name: 'Ada', avatarUrl: null }
    const fetchSpy = mockFetch(
      new Response(JSON.stringify(user), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )

    const result = await apiRequest<typeof user>('/auth/me')

    expect(result).toEqual(user)

    const [url, init] = fetchSpy.mock.calls[0]
    // El prefijo lo pone el wrapper, no quien llama.
    expect(url).toBe('/api/auth/me')
    // Sin esto el navegador no manda la cookie `access_token` y la API responde 401.
    expect(init.credentials).toBe('include')
    expect(init.method).toBe('GET')
  })

  it('serializa el body y solo declara Content-Type cuando lo hay', async () => {
    const withBody = mockFetch(new Response(null, { status: 204 }))

    await apiRequest('/logs', { method: 'POST', body: { logDate: '2026-09-30' } })

    const init = withBody.mock.calls[0][1]
    expect(init.method).toBe('POST')
    expect(init.body).toBe('{"logDate":"2026-09-30"}')
    expect(headersOf(withBody)).toEqual({ 'Content-Type': 'application/json' })

    vi.unstubAllGlobals()
    const withoutBody = mockFetch(new Response(null, { status: 204 }))

    await apiRequest('/auth/me')

    // Un GET sin cuerpo no lleva Content-Type: anunciarlo sin mandar nada
    // confunde a cualquier proxy que eche un vistazo a la petición.
    expect(headersOf(withoutBody)).toEqual({})
  })

  it('devuelve undefined en un 204 sin cuerpo en vez de lanzar', async () => {
    // `response.json()` sobre un cuerpo vacío revienta con un SyntaxError, así
    // que este caso es el que cubre POST /api/auth/logout.
    mockFetch(new Response(null, { status: 204 }))

    await expect(apiRequest('/auth/logout', { method: 'POST' })).resolves.toBeUndefined()
  })

  it('lanza ApiError con status, code y message del sobre error', async () => {
    const body = {
      error: {
        code: 'VALIDATION_ERROR',
        message: 'One or more validation errors occurred.',
        details: [{ field: 'MoodRating', message: "'Mood Rating' must be between 1 and 5." }],
      },
    }
    mockFetch(
      new Response(JSON.stringify(body), {
        status: 400,
        headers: { 'Content-Type': 'application/json' },
      }),
    )

    const error = await captureError(apiRequest('/logs', { method: 'POST', body: {} }))

    expect(error).toBeInstanceOf(Error)
    expect(error.status).toBe(400)
    expect(error.code).toBe('VALIDATION_ERROR')
    expect(error.message).toBe('One or more validation errors occurred.')
    expect(error.details).toEqual(body.error.details)
  })

  it('degrada a INTERNAL_ERROR cuando el cuerpo del error no es parseable', async () => {
    // Lo que devuelve un proxy delante de la API: HTML, no JSON.
    mockFetch(
      new Response('<html><body>502 Bad Gateway</body></html>', {
        status: 502,
        statusText: 'Bad Gateway',
        headers: { 'Content-Type': 'text/html' },
      }),
    )

    const error = await captureError(apiRequest('/logs'))

    // Lo importante es que siga siendo un ApiError con el status: un JSON.parse
    // a pelo tiraría un SyntaxError y la UI perdería el status para decidir a
    // dónde llevar al usuario.
    expect(error.status).toBe(502)
    expect(error.code).toBe('INTERNAL_ERROR')
    expect(error.message).toBe('Bad Gateway')
    expect(error.details).toEqual([])
  })
})
