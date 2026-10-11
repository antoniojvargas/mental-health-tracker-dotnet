import vue from '@vitejs/plugin-vue'
import type { OutputChunk } from 'rolldown'
import { defineConfig, loadEnv, type Plugin } from 'vite'

/**
 * Aborta el build si una librería diferida acaba en el chunk inicial.
 *
 * Chart.js y el cliente de SignalR viven detrás de la carga diferida de
 * `/dashboard` (ver `src/router/index.ts`): quien aterriza en /login no debería
 * descargarlos. Un import estático accidental —desde App.vue, un store o un
 * componente compartido— los arrastraría al chunk inicial sin que nada lo
 * delate: el tamaño sube, pero el build sigue verde. Aquí se recorre el grafo de
 * módulos de Rollup y se aborta si aparecen en un chunk alcanzable desde un
 * entry por import estático.
 */
function sinLibreriasDiferidasEnElInicial(): Plugin {
  const prohibidas = ['/node_modules/chart.js/', '/node_modules/@microsoft/signalr/']

  return {
    name: 'sin-librerias-diferidas-en-el-inicial',
    apply: 'build',
    generateBundle(_options, bundle) {
      const chunks = Object.values(bundle).filter(
        (salida): salida is OutputChunk => salida.type === 'chunk',
      )

      // Inicial = todo lo alcanzable por import estático desde un entry: eso es
      // lo que el navegador descarga al abrir la app. Lo que solo entra por
      // import dinámico (isDynamicEntry) llega después, al abrir esa vista, así
      // que ahí estas librerías sí son legítimas.
      const iniciales = new Set<string>()
      const visitar = (chunk: OutputChunk | undefined) => {
        if (!chunk || iniciales.has(chunk.fileName)) return
        iniciales.add(chunk.fileName)
        for (const dependencia of chunk.imports) {
          visitar(bundle[dependencia] as OutputChunk)
        }
      }
      for (const chunk of chunks) if (chunk.isEntry) visitar(chunk)

      const fugas: string[] = []
      for (const chunk of chunks) {
        if (!iniciales.has(chunk.fileName)) continue
        for (const id of chunk.moduleIds) {
          // Los ids traen separadores nativos; se normalizan para no depender de
          // la plataforma donde se construya.
          const ruta = id.replaceAll('\\', '/')
          if (prohibidas.some((patron) => ruta.includes(patron))) {
            fugas.push(`${ruta} → ${chunk.fileName}`)
          }
        }
      }

      if (fugas.length > 0) {
        this.error(
          'Chart.js / SignalR acabarían en el chunk inicial: deberían ir con la ' +
            'carga diferida del dashboard.\n' +
            fugas.map((fuga) => `  - ${fuga}`).join('\n'),
        )
      }
    },
  }
}

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  // Ojo: este target vale para `npm run dev` en el host, donde el backend sí
  // escucha en localhost:3000. Dentro de la red de Compose hay que llegar por
  // nombre de servicio (http://backend:3000), porque allí localhost apunta al
  // propio contenedor del frontend. Por eso VITE_PROXY_TARGET es configurable.
  const target = loadEnv(mode, process.cwd()).VITE_PROXY_TARGET || 'http://localhost:3000'

  return {
    plugins: [vue(), sinLibreriasDiferidasEnElInicial()],
    server: {
      proxy: {
        '/api': { target, changeOrigin: true },
        '/hub': { target, changeOrigin: true, ws: true },
      },
    },
  }
})
