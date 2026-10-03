/**
 * Suscripción al hub de registros durante la vida de un componente.
 *
 * Al montar registra un mismo manejador para `log:created` y `log:updated` y
 * arranca la conexión compartida de hub-client.ts; al desmontar retira ESE
 * manejador (misma referencia, que es lo que evita dejarlo colgado) de ambos
 * eventos y detiene la conexión.
 *
 * No se reconecta ni reintenta el arranque inicial a mano: si `start()` falla
 * —servidor caído, sesión caducada—, la conexión se queda detenida y la
 * reconexión automática del hub no aplica hasta que haya una conexión viva.
 */

import { onMounted, onUnmounted } from 'vue'

import { getLogHubConnection } from '../services/hub-client'
import type { DailyLog } from '../types/daily-log'

export function useLogSocket(onLog: (log: DailyLog) => void): void {
  const connection = getLogHubConnection()
  const handleLog = (log: DailyLog) => onLog(log)

  onMounted(() => {
    connection.on('log:created', handleLog)
    connection.on('log:updated', handleLog)
    void connection.start()
  })

  onUnmounted(() => {
    connection.off('log:created', handleLog)
    connection.off('log:updated', handleLog)
    void connection.stop()
  })
}
