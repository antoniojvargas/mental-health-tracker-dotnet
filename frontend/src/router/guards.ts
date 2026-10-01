/**
 * Guard de sesión.
 *
 * Vive fuera de router/index.ts a propósito: importar el router crea el router
 * de verdad, con su historia web, así que un guard definido dentro no se podría
 * probar sin arrastrar ese efecto. Aquí solo se exporta la función.
 *
 * El flag va en el `meta` de la ruta y no en una lista dentro del guard: así
 * "qué rutas exigen sesión" se lee en la tabla de rutas, y añadir la siguiente
 * ruta protegida es añadir una línea, no tocar esta lógica.
 */

import type { NavigationGuard, RouteLocationNormalized } from 'vue-router'

import { useAuthStore } from '../stores/useAuthStore'

declare module 'vue-router' {
  interface RouteMeta {
    /** La ruta exige una sesión resuelta antes de dejar pasar la navegación. */
    requiresAuth?: boolean
  }
}

/** Destino cuando no hay sesión. Sin `meta.requiresAuth`, para no abrir un bucle. */
const LOGIN_ROUTE = '/login'

/**
 * Espera a que la sesión se resuelva y, si no hay usuario, manda a /login.
 *
 * Se espera de verdad, con `await`: es lo que evita el fogonazo de ver el
 * dashboard una fracción de segundo antes de saltar a /login. `fetchMe()`
 * devuelve `null` en vez de lanzar cuando la respuesta es 401, que es el caso
 * normal de quien no ha entrado, así que el 401 no hay que tratarlo aquí.
 *
 * Devolver la ruta de destino en vez de `next()` es lo que quiere vue-router 5:
 * el callback está deprecado y disappear en una versión futura.
 *
 * Un error que no sea 401 sube y aborta la navegación. Redirecting a /login
 * en ese caso sería mentir: una caída de la API no es que el usuario se haya
 * quedado sin sesión, y mandarle a la pantalla de login puede hacer que cierre
 * sesiones que siguen vivas. Se deja la puerta a que exista una pantalla de
 * error antes de decidir otra cosa.
 */
export const requireAuth: NavigationGuard = async (to: RouteLocationNormalized) => {
  if (!to.meta.requiresAuth) {
    return true
  }

  const user = await useAuthStore().fetchMe()
  return user ? true : LOGIN_ROUTE
}
