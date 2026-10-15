import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { RouteLocationNormalized } from 'vue-router'

import { requireAuth } from './guards'
import { ApiError } from '../services/api-client'
import { useAuthStore } from '../stores/useAuthStore'
import { captureError, emptyResponse, jsonResponse, mockFetch } from '../test-support/http'

const USER = { id: 'a1', email: 'ada@example.com', name: 'Ada', avatarUrl: null }

function route(meta: RouteLocationNormalized['meta'] = {}): RouteLocationNormalized {
  return { path: '/x', meta } as RouteLocationNormalized
}

/** El guard recibe (to, from, next); con vue-router 5 solo importa `to` y el valor devuelto. */
function run(to: RouteLocationNormalized) {
  return (requireAuth as (to: RouteLocationNormalized) => Promise<unknown>)(to)
}

describe('requireAuth', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.unstubAllGlobals()
  })

  it('una ruta sin meta.requiresAuth pasa sin consultar la sesión', async () => {
    const fetchSpy = mockFetch(jsonResponse(USER))

    await expect(run(route())).resolves.toBe(true)
    await expect(run(route({ requiresAuth: false }))).resolves.toBe(true)

    expect(fetchSpy).not.toHaveBeenCalled()
  })

  it('con sesión (fetchMe devuelve el usuario) deja pasar', async () => {
    mockFetch(jsonResponse(USER))

    await expect(run(route({ requiresAuth: true }))).resolves.toBe(true)
    expect(useAuthStore().user).toEqual(USER)
  })

  it('con 401 con sobre redirige a /login', async () => {
    mockFetch(
      jsonResponse(
        { error: { code: 'UNAUTHORIZED', message: 'Authentication required', details: [] } },
        401,
      ),
    )

    await expect(run(route({ requiresAuth: true }))).resolves.toBe('/login')
    expect(useAuthStore().user).toBeNull()
  })

  it('con 401 sin cuerpo (p. ej. de un proxy) también redirige a /login', async () => {
    mockFetch(emptyResponse(401, 'Unauthorized'))

    await expect(run(route({ requiresAuth: true }))).resolves.toBe('/login')
  })

  it('un 500 se propaga y no se confunde con "sin sesión"', async () => {
    mockFetch(
      jsonResponse({ error: { code: 'INTERNAL_ERROR', message: 'boom', details: [] } }, 500),
    )

    const error = await captureError(run(route({ requiresAuth: true })))

    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(500)
  })

  it('un fallo de red también se propaga', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

    await expect(run(route({ requiresAuth: true }))).rejects.toBeInstanceOf(TypeError)
  })

  it('espera a que la sesión se resuelva antes de decidir', async () => {
    let resolve!: (response: Response) => void
    vi.stubGlobal('fetch', vi.fn().mockReturnValue(new Promise<Response>((r) => (resolve = r))))

    let settled = false
    const result = run(route({ requiresAuth: true })).then((value) => {
      settled = true
      return value
    })
    await Promise.resolve()
    expect(settled).toBe(false)

    resolve(jsonResponse(USER))

    await expect(result).resolves.toBe(true)
  })
})
