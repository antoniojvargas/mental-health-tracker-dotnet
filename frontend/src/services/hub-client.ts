/**
 * Cliente del hub de SignalR de registros (`/hub/logs`).
 *
 * Expone una única conexión, creada de forma perezosa la primera vez que se pide
 * y reutilizada después. Construirla NO conecta: el `HubConnection` se queda
 * detenido hasta que alguien llame a `start()`, para que ningún import del
 * módulo abra un socket por su cuenta.
 *
 * La sesión viaja en la cookie httpOnly `access_token` (igual que en
 * api-client.ts), así que la conexión necesita `withCredentials` para que el
 * navegador la mande; sin eso el hub responde 401.
 *
 * La ruta va relativa al origen, sin el prefijo `/api`: el proxy de Vite y el de
 * producción reenvían `/hub` (con soporte de WebSocket) contra el backend.
 */

import { HubConnectionBuilder, type HubConnection } from '@microsoft/signalr'

let connection: HubConnection | null = null

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
      .withUrl('/hub/logs', { withCredentials: true })
      .withAutomaticReconnect()
      .build()
  }

  return connection
}
