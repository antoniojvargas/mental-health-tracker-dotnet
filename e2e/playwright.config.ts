import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  reporter: 'html',
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    // La imagen real de produccion (nginx + bundle compilado) en el puerto 8080, no el
    // servidor de Vite. Solo el recorrido critico, con el fixture que falla ante
    // cualquier violacion de CSP. Se ejecuta con `npm run test:prod-smoke` contra la
    // pila de docker-compose.prod.e2e.yml.
    {
      name: 'prod-smoke',
      testMatch: /critical-flow\.spec\.ts/,
      use: { ...devices['Desktop Chrome'], baseURL: 'http://localhost:8080' },
    },
  ],
})
