import { defineConfig } from 'vitest/config'

// Config propio y no el de vite.config.ts: un test de TypeScript puro no
// necesita el plugin de Vue, y arrastrar el proxy de /api y /hub aquí solo
// estorbaría. `environment: 'node'` porque el cliente no toca el DOM: lo que
// usa del navegador (fetch, Response) ya está en el global de Node 22.
export default defineConfig({
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
})
