// @vitest-environment jsdom
/**
 * Tests de componente de LoginView.
 *
 * Monta la vista de verdad, con un router de memoria y un Pinia reales, y lo
 * único que se sustituye es `fetch`. La razón es que la lógica que interesa
 * aquí es justo la que une las tres piezas — el `query` de la ruta, el store y el
 * `replace` del router—, así que falsificar el router o el store sería dejar el
 * cableado real fuera del test: la suite pasaría mientras la vista estuviera rota.
 *
 * El docblock de arriba cambia el entorno solo para este fichero. El global
 * sigue siendo `node` porque los tests del cliente HTTP y del store no tocan el
 * DOM, y pagarlos con jsdom sería ralentizarlos sin ganar nada.
 */

import { describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { createMemoryHistory, createRouter, type Router } from 'vue-router'

import LoginView from '../LoginView.vue'
import { emptyResponse, jsonResponse, mockFetch } from '../../test-support/http'

const USER = { id: 'a1', email: 'ada@example.com', name: 'Ada', avatarUrl: null }

/**
 * Router de memoria con las dos rutas que la vista toca: `/login`, de donde sale
 * el `query`, y `/dashboard`, destino del `replace`. El destino hace falta
 * aunque el test no lo monte: `router.replace` contra una ruta inexistente
 * rechaza, y ese rechazo se colaría como si fuera un fallo de la vista.
 *
 * Deliberadamente NO se registra `requireAuth` como guard. El guard ya tiene sus
 * propios tests, y aquí reventaría al test del 500: `fetch` está stubbeado para
 * un único uso, así que la segunda llamada devolvería la misma respuesta y el
 * guard se llevaría por delante la aserción de la vista. El 500 se comprueba
 * igualmente porque es la vista quien decide no redirigir.
 */
async function routerAt(url: string): Promise<Router> {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/login', component: LoginView },
      { path: '/dashboard', component: { template: '<div />' } },
    ],
  })
  // Hay que resolver `isReady` antes de montar: `useRoute` lee la ruta activa en
  // el setup, y sin esto el primer render vería la ruta inicial en vez de la que
  // se le pidió. Sin este await, el aviso de error se probaría siempre en falso.
  await router.push(url)
  await router.isReady()
  return router
}

/** Monta la vista y devuelve el wrapper con el router, para poder inspeccionarlo. */
async function mountAt(url: string) {
  const router = await routerAt(url)
  const wrapper = mount(LoginView, { global: { plugins: [createPinia(), router] } })
  // Deja correr el `onMounted`: ahí es donde la vista pregunta por la sesión.
  await flushPromises()
  return { wrapper, router }
}

describe('LoginView', () => {
  describe('el enlace de Google', () => {
    it('se renderiza apuntando a /api/auth/google, y no a un router-link', async () => {
      const { wrapper } = await mountAt('/login')

      const link = wrapper.get('a[href="/api/auth/google"]')

      expect(link.text()).toContain('Continue with Google')
      // El flujo se resuelve con una cookie httpOnly que fija el backend, así que
      // tiene que ser una navegación de página entera. Un router-link resolvería
      // la autenticación en el cliente, donde esa cookie no llega.
      expect(link.attributes('href')).toBe('/api/auth/google')
    })

    it('es el único enlace de la vista, para que la pestaña de Google sea la salida', async () => {
      const { wrapper } = await mountAt('/login')

      expect(wrapper.findAll('a')).toHaveLength(1)
    })
  })

  describe('el aviso de error', () => {
    it('aparece con role="alert" cuando el query trae error=auth_failed', async () => {
      const { wrapper } = await mountAt('/login?error=auth_failed')

      const alert = wrapper.get('[role="alert"]')

      // `role="alert"` implica `aria-live="assertive"`: sin él el texto se ve pero
      // un lector de pantalla pasa de largo, que es justo quien lo necesita.
      expect(alert.text()).toBe("We couldn't sign you in with Google. Please try again.")
    })

    it('no aparece sin el query param', async () => {
      const { wrapper } = await mountAt('/login')

      expect(wrapper.find('[role="alert"]').exists()).toBe(false)
    })

    it('no aparece con un código de error desconocido, antes que mostrar el mensaje equivocado', async () => {
      // El backend solo emite `auth_failed`, pero un parámetro adulterado a mano
      // no debe poder parar a alguien con el mensaje de otro fallo.
      const { wrapper } = await mountAt('/login?error=oauth_token_exchange_failed')

      expect(wrapper.find('[role="alert"]').exists()).toBe(false)
    })

    it('el enlace de Google sigue disponible para reintentar', async () => {
      // Si el aviso apareciera tapando el botón, el usuario se quedaría sin
      // salida y solo le quedaría recargar a mano.
      const { wrapper } = await mountAt('/login?error=auth_failed')

      expect(wrapper.get('a[href="/api/auth/google"]').isVisible()).toBe(true)
    })
  })

  describe('la redirección con sesión ya abierta', () => {
    it('manda a /dashboard cuando la API devuelve un usuario', async () => {
      mockFetch(jsonResponse(USER))

      const { router } = await mountAt('/login')

      expect(router.currentRoute.value.path).toBe('/dashboard')
    })

    it('reemplaza el historial en vez de apilar, para que el botón de atrás no bucle', async () => {
      mockFetch(jsonResponse(USER))
      const router = await routerAt('/login')
      // Se espía en vez de leer `history.state`: el historial en memoria no
      // lleva la pila de entradas, así que `state.back` no dice nada. Lo que
      // importa es qué método llama la vista, y el espía deja la navegación
      // real ocurre igual — solo la observa.
      const replace = vi.spyOn(router, 'replace')
      const push = vi.spyOn(router, 'push')

      mount(LoginView, { global: { plugins: [createPinia(), router] } })
      await flushPromises()

      // Si en el historial quedara /login, el botón de atrás traería al usuario
      // aquí, el guard lo devolvería a /dashboard, y pelearía con su propio
      // historial. `replace` es lo que lo evita.
      expect(replace).toHaveBeenCalledWith('/dashboard')
      expect(push).not.toHaveBeenCalled()
    })

    it('se queda en /login cuando la API responde 401, que es el caso de un visitante', async () => {
      // El store resuelve el 401 como "no hay sesión", no como fallo.
      mockFetch(emptyResponse(401, 'Unauthorized'))

      const { router, wrapper } = await mountAt('/login')

      expect(router.currentRoute.value.path).toBe('/login')
      // La tarjeta sigue en pantalla: `get` revienta si el enlace no estuviera,
      // que es justo lo que hay que comprobar.
      expect(wrapper.get('a[href="/api/auth/google"]').isVisible()).toBe(true)
    })

    it('se queda en /login si la API falla, en vez de dejar la pantalla vacía', async () => {
      // La vista se come el error a propósito: si la API está caída, redirigir
      // no lleva a ninguna parte y la tarjeta de login tiene que seguir sirviendo.
      // Un rechazo sin capturar aquí sería un unhandled rejection en el test.
      mockFetch(emptyResponse(500, 'Internal Server Error'))

      const { router, wrapper } = await mountAt('/login')

      expect(router.currentRoute.value.path).toBe('/login')
      // Igual que arriba: si la tarjeta hubiera desaparecido, `get` lo delata.
      expect(wrapper.get('a[href="/api/auth/google"]').isVisible()).toBe(true)
    })

    it('redirige aunque el aviso de error siga en la URL, porque manda la sesión', async () => {
      // Un `?error=auth_failed` viejo en la URL no debe ganarle a una cookie
      // válida: el servidor de Google ya había iniciado sesión y el aviso es un resto.
      mockFetch(jsonResponse(USER))

      const { router } = await mountAt('/login?error=auth_failed')

      expect(router.currentRoute.value.path).toBe('/dashboard')
    })
  })
})
