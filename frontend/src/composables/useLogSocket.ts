/**
 * Suscripción al hub de registros durante la vida de un componente.
 *
 * Al montar registra un mismo manejador para `log:created` y `log:updated` y
 * arranca la conexión compartida de hub-client.ts; al desmontar retira ESE
 * manejador (misma referencia, que es lo que evita dejarlo colgado) de ambos
 * eventos y detiene la conexión.
 *
 * No se reintenta el arranque inicial a mano: si `start()` falla —servidor
 * caído, sesión caducada—, la conexión se queda detenida y la reconexión
 * automática del hub no aplica hasta que haya una conexión viva.
 *
 * El aviso en tiempo real es de mejor esfuerzo: los eventos emitidos mientras la
 * conexión estaba caída no se reenvían. Por eso, cuando el hub se reconecta solo,
 * se llama a `onReconnected` para que quien usa el composable vuelva a pedir los
 * datos a la API, que es la fuente de verdad. SignalR no permite retirar un
 * callback de `onreconnected`, así que se registra uno por montaje y una bandera
 * lo silencia al desmontar.
 */

import { onMounted, onUnmounted } from 'vue'

import { getLogHubConnection } from '../services/hub-client'
import type { DailyLog } from '../types/daily-log'

export function useLogSocket(onLog: (log: DailyLog) => void, onReconnected?: () => void): void {
  const connection = getLogHubConnection()
  const handleLog = (log: DailyLog) => onLog(log)
  let mounted = false

  onMounted(() => {
    mounted = true
    if (onReconnected) {
      connection.onreconnected(() => {
        if (mounted) onReconnected()
      })
    }
    connection.on('log:created', handleLog)
    connection.on('log:updated', handleLog)
    void connection.start()
  })

  onUnmounted(() => {
    mounted = false
    connection.off('log:created', handleLog)
    connection.off('log:updated', handleLog)
    void connection.stop()
  })
}
