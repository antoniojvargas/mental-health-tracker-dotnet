import { createRouter, createWebHistory } from 'vue-router'

import { requireAuth } from './guards'
import LoginView from '../views/LoginView.vue'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/dashboard' },
    { path: '/login', component: LoginView },
    {
      path: '/dashboard',
      // diferido a propósito: las gráficas y el cliente de tiempo real cuelgan
      // de aquí, y mantenerlos fuera del bundle inicial evita descargarlos en
      // quien aterrice en /login.
      component: () => import('../views/DashboardView.vue'),
      // El guard espera a que fetchMe resuelva antes de dejar entrar, así que
      // /dashboard nunca se pinta sin saber si hay sesión detrás.
      meta: { requiresAuth: true },
    },
  ],
})

// Un único guard global que mira el `meta` de la ruta, en vez de uno por ruta:
// /login se queda sin `requiresAuth` a propósito, y si el flag estuviera en una
// lista dentro del guard habría que acordarse de no añadirla ahí.
router.beforeEach(requireAuth)
