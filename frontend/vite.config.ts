import vue from '@vitejs/plugin-vue'
import { defineConfig, loadEnv } from 'vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  // Ojo: este target vale para `npm run dev` en el host, donde el backend sí
  // escucha en localhost:3000. Dentro de la red de Compose hay que llegar por
  // nombre de servicio (http://backend:3000), porque allí localhost apunta al
  // propio contenedor del frontend. Por eso VITE_PROXY_TARGET es configurable.
  const target = loadEnv(mode, process.cwd()).VITE_PROXY_TARGET || 'http://localhost:3000'

  return {
    plugins: [vue()],
    server: {
      proxy: {
        '/api': { target, changeOrigin: true },
        '/hub': { target, changeOrigin: true, ws: true },
      },
    },
  }
})
