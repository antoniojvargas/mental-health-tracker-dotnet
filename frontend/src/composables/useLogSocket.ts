/**
 * Suscripción al hub de registros durante la vida de un componente.
 *
 * Al montar registra un mismo manejador para `log:created` y `log:updated` y
 * toma la conexión compartida de hub-client.ts (`acquireLogHub`, que la arranca si es
 * el primer consumidor); al desmontar retira ESE manejador (misma referencia, que es lo
 * que evita dejarlo colgado) de ambos eventos y la suelta (`releaseLogHub`, que la detiene
 * si era el último).
 *
 * No se reintenta el arranque inicial a mano: si `start()` falla —servidor
 * caído, sesión caducada—, la conexión se queda detenida y la reconexión
 * automática del hub no aplica hasta que haya una conexión viva. El rechazo se
 * captura y se registra con `console.warn` para que no quede sin manejar.
 *
 * El aviso en tiempo real es de mejor esfuerzo: los eventos emitidos mientras la
 * conexión estaba caída no se reenvían. Por eso, cuando el hub se reconecta solo,
 * se llama a `onReconnected` para que quien usa el composable vuelva a pedir los
 * datos a la API, que es la fuente de verdad. SignalR no permite retirar un
 * callback de `onreconnected`, así que se registra uno por montaje y una bandera
 * lo silencia al desmontar.
 */

import type { HubConnection } from '@microsoft/signalr'
import { onMounted, onUnmounted } from 'vue'

import { acquireLogHub, releaseLogHub } from '../services/hub-client'
import { LOG_HUB_EVENTS } from '../services/hub-events'
import type { DailyLog } from '../types/daily-log'

export function useLogSocket(onLog: (log: DailyLog) => void, onReconnected?: () => void): void {
  let connection: HubConnection | null = null
  const handleLog = (log: DailyLog) => onLog(log)
  let mounted = false

  onMounted(() => {
    mounted = true
    // acquireLogHub arranca la conexión si es la primera; start() es asíncrono, así que los
    // manejadores quedan registrados antes de que llegue ningún evento.
    const { connection: hub, started } = acquireLogHub()
    connection = hub
    if (onReconnected) {
      hub.onreconnected(() => {
        if (mounted) onReconnected()
      })
    }
    hub.on(LOG_HUB_EVENTS.created, handleLog)
    hub.on(LOG_HUB_EVENTS.updated, handleLog)
    // Sin reintento: si el arranque falla la conexión queda detenida. Se captura el rechazo
    // para que no salga como unhandled rejection; la API sigue siendo la fuente de verdad.
    started.catch((error: unknown) => {
      console.warn('No se pudo arrancar la conexión en tiempo real', error)
    })
  })

  onUnmounted(() => {
    mounted = false
    if (!connection) return

    connection.off(LOG_HUB_EVENTS.created, handleLog)
    connection.off(LOG_HUB_EVENTS.updated, handleLog)
    connection = null
    releaseLogHub()
  })
}
