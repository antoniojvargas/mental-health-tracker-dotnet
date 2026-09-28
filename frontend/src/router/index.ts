import { createRouter, createWebHistory } from 'vue-router'

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
    },
  ],
})
