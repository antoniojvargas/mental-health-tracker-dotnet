import { beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { useAuthStore } from './useAuthStore'
import { captureError, emptyResponse, jsonResponse, mockFetch } from '../test-support/http'

const USER = { id: 'a1', email: 'ada@example.com', name: 'Ada', avatarUrl: null }

describe('useAuthStore', () => {
  beforeEach(() => {
    // Un pinia por test: el store es un singleton dentro de la app, pero entre
    // tests cada uno necesita el suyo para no heredar el `user` del anterior.
    setActivePinia(createPinia())
  })

  it('guarda el usuario con la sesión válida y marca loading solo mientras vuela', async () => {
    mockFetch(jsonResponse(USER))
    const store = useAuthStore()

    expect(store.user).toBeNull()
    expect(store.loading).toBe(false)

    const pending = store.fetchMe()
    expect(store.loading).toBe(true)

    expect(await pending).toEqual(USER)
    expect(store.user).toEqual(USER)
    // El `finally` tiene que bajar la bandera también cuando todo va bien, o la
    // vista se quedaría en spinner para siempre.
    expect(store.loading).toBe(false)
  })

  it('trata el 401 con sobre como ausencia de sesión, sin lanzar', async () => {
    // Lo emite RequireAuthMiddleware a través del manejador global.
    mockFetch(
      jsonResponse(
        { error: { code: 'UNAUTHORIZED', message: 'Authentication required', details: [] } },
        401,
      ),
    )
    const store = useAuthStore()

    // Un visitante sin cookie es el caso normal, no un fallo que reportar.
    await expect(store.fetchMe()).resolves.toBeNull()
    expect(store.user).toBeNull()
    expect(store.loading).toBe(false)
  })

  it('trata el 401 sin sobre como ausencia de sesión, sin lanzar', async () => {
    // El otro camino del mismo 401: AuthController.Me responde con un
    // `Unauthorized()` a secas cuando la cookie vale pero el usuario ya no
    // existe. El cuerpo va vacío, así que apiRequest lo degrada a
    // INTERNAL_ERROR: por eso el store decide por el status y no por el code.
    mockFetch(emptyResponse(401, 'Unauthorized'))
    const store = useAuthStore()

    await expect(store.fetchMe()).resolves.toBeNull()
    expect(store.user).toBeNull()
  })

  it('propaga un error inesperado en vez de tragárselo', async () => {
    // Si la API está caída, alguien tiene que verlo; tragarse el fallo dejaría
    // una pantalla vacía sin explicación.
    mockFetch(emptyResponse(500, 'Internal Server Error'))
    const store = useAuthStore()

    const error = await captureError(store.fetchMe())

    expect(error.status).toBe(500)
    // Nada que inventar: el fallo no se convierte en "no hay sesión".
    expect(store.user).toBeNull()
    expect(store.loading).toBe(false)
  })

  it('limpia el usuario tras un logout correcto', async () => {
    mockFetch(jsonResponse(USER))
    const store = useAuthStore()
    await store.fetchMe()
    expect(store.user).toEqual(USER)

    // El logout responde 204, sin cuerpo.
    mockFetch(emptyResponse(204))
    await store.logout()

    expect(store.user).toBeNull()
  })

  it('no limpia el usuario si el logout falla, porque la sesión sigue viva', async () => {
    mockFetch(jsonResponse(USER))
    const store = useAuthStore()
    await store.fetchMe()

    mockFetch(emptyResponse(500, 'Internal Server Error'))
    await captureError(store.logout())

    // El backend no llegó a borrar la cookie, así que vaciar el store sería
    // mentirle al usuario: recargando volvería a estar dentro.
    expect(store.user).toEqual(USER)
  })
})
