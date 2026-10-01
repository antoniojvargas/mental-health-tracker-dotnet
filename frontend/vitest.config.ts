import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vitest/config'

// Config propio y no el de vite.config.ts: arrastrar el proxy de /api y /hub
// aquí solo estorbaría. `environment: 'node'` porque el cliente HTTP y el store
// no tocan el DOM: lo que usan del navegador (fetch, Response) ya está en el
// global de Node 22.
//
// El plugin de Vue sí hace falta, pero no en todo. Sin él, un test que importa
// un `.vue` falla al parsearlo. Se registra solo aquí, en vez de heredar el
// `vite.config.ts`, porque el proxy de desarrollo y el SignalR no tienen nada
// que ver con una suite de tests.
export default defineConfig({
  plugins: [vue()],
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts'],
    // Restituye los `vi.stubGlobal` solo entre tests, para que ninguno tenga que
    // acordarse de un `afterEach` y el `fetch` de un test no se cuele al siguiente.
    unstubGlobals: true,
  },
})
