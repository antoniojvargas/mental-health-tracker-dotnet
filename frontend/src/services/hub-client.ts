/**
 * Cliente del hub de SignalR de registros (`/hub/logs`).
 *
 * Expone una única conexión, creada de forma perezosa la primera vez que se pide
 * y reutilizada después. Construirla NO conecta: el `HubConnection` se queda
 * detenido hasta que el primer consumidor llama a `acquireLogHub()`, para que ningún
 * import del módulo abra un socket por su cuenta. Los consumidores se cuentan: la
 * conexión se detiene cuando el último llama a `releaseLogHub()`.
 *
 * La sesión viaja en la cookie httpOnly `access_token` (igual que en
 * api-client.ts), así que la conexión necesita `withCredentials` para que el
 * navegador la mande; sin eso el hub responde 401.
 *
 * La ruta (hub-events.ts) va relativa al origen, sin el prefijo `/api`: el proxy de Vite y el de
 * producción reenvían `/hub` (con soporte de WebSocket) contra el backend.
 */

import { HubConnectionBuilder, type HubConnection } from '@microsoft/signalr'

import { LOGS_HUB_PATH } from './hub-events'

let connection: HubConnection | null = null
let consumers = 0
let started: Promise<void> = Promise.resolve()

/**
 * Devuelve la conexión al hub, creándola la primera vez.
 *
 * Queda con reconexión automática activada (los reintentos por defecto de
 * SignalR: 0, 2, 10 y 30 segundos), pero desconectada: conectar es
 * responsabilidad de quien la use, no de este módulo.
 */
export function getLogHubConnection(): HubConnection {
  if (!connection) {
    connection = new HubConnectionBuilder()
      .withUrl(LOGS_HUB_PATH, { withCredentials: true })
      .withAutomaticReconnect()
      .build()
  }

  return connection
}

/**
 * Pide la conexión como consumidor: la arranca el primero y las demás peticiones la
 * comparten ya en marcha. Cada `acquireLogHub` debe emparejarse con un `releaseLogHub`.
 *
 * Devuelve la conexión y `started`, la promesa del arranque compartido: se rechaza si el
 * `start()` falla (servidor caído, sesión caducada) y quien la use decide qué hacer con el
 * error. Aquí no se reintenta: la conexión queda detenida, con uno o con varios consumidores.
 */
export function acquireLogHub(): { connection: HubConnection; started: Promise<void> } {
  const connection = getLogHubConnection()
  consumers += 1
  if (consumers === 1) started = connection.start()

  return { connection, started }
}

/**
 * Devuelve la conexión: el último consumidor en soltarla la detiene. Soltar de más (sin un
 * `acquireLogHub` pendiente) no hace nada, para que un desmontaje duplicado no pare a nadie.
 */
export function releaseLogHub(): void {
  if (consumers === 0) return

  consumers -= 1
  if (consumers === 0) void getLogHubConnection().stop()
}
