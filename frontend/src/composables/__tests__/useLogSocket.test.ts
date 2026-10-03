// @vitest-environment jsdom
/**
 * Tests de `useLogSocket`.
 *
 * Corre en `jsdom` porque el composable cuelga de `onMounted`/`onUnmounted`, y
 * para eso hay que montar un componente. El hub se sustituye por un cliente
 * falso: aquí se prueba el ciclo de vida y el reenvío del registro, no SignalR.
 *
 * La limpieza fina importa: conectar al montar no basta si al desmontar quedan
 * manejadores colgados en los eventos, así que los asserts de `off` comparan la
 * MISMA referencia de función que se registró en `on`.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h } from 'vue'

import { useLogSocket } from '../useLogSocket'
import { getLogHubConnection } from '../../services/hub-client'
import type { DailyLog, IsoDate } from '../../types/daily-log'

vi.mock('../../services/hub-client', () => ({
  getLogHubConnection: vi.fn(),
}))

type ConnectionHandler = (log: DailyLog) => void

const connection = {
  on: vi.fn<(event: string, handler: ConnectionHandler) => void>(),
  off: vi.fn<(event: string, handler: ConnectionHandler) => void>(),
  start: vi.fn<() => Promise<void>>().mockResolvedValue(undefined),
  stop: vi.fn<() => Promise<void>>().mockResolvedValue(undefined),
}

function makeLog(logDate: IsoDate): DailyLog {
  return {
    id: `id-${logDate}`,
    logDate,
    moodRating: 3,
    anxietyLevel: 5,
    stressLevel: 5,
    sleepHours: 7,
    sleepQuality: 4,
    sleepDisturbances: [],
    activityType: null,
    activityMinutes: null,
    socialFrequency: 'none',
    symptoms: [],
    notes: null,
    createdAt: '2026-09-13T17:18:21.1234567+00:00',
    updatedAt: '2026-09-13T17:18:21.1234567+00:00',
  }
}

let wrapper: VueWrapper | null = null

function mountSocket(onLog: ConnectionHandler): VueWrapper {
  const Harness = defineComponent({
    setup() {
      useLogSocket(onLog)
      return () => h('div')
    },
  })
  wrapper = mount(Harness)
  return wrapper
}

/** El manejador que quedó registrado para un evento, para poder invocarlo. */
function handlerFor(event: string): ConnectionHandler {
  const call = connection.on.mock.calls.find(([name]) => name === event)
  if (!call) throw new Error(`Sin manejador registrado para ${event}`)
  return call[1]
}

describe('useLogSocket', () => {
  beforeEach(() => {
    vi.mocked(getLogHubConnection).mockReturnValue(connection as never)
    connection.on.mockClear()
    connection.off.mockClear()
    connection.start.mockClear()
    connection.stop.mockClear()
  })

  afterEach(() => {
    wrapper?.unmount()
    wrapper = null
  })

  it('al montar arranca la conexión y se suscribe a los dos eventos', () => {
    mountSocket(vi.fn())

    expect(connection.start).toHaveBeenCalledTimes(1)
    expect(connection.on).toHaveBeenCalledWith('log:created', expect.any(Function))
    expect(connection.on).toHaveBeenCalledWith('log:updated', expect.any(Function))
  })

  it('registra el mismo manejador en ambos eventos, para poder retirarlo luego', () => {
    mountSocket(vi.fn())

    expect(handlerFor('log:created')).toBe(handlerFor('log:updated'))
  })

  it('propaga el registro que llega por log:created', () => {
    const onLog = vi.fn()
    mountSocket(onLog)
    const log = makeLog('2026-08-01')

    handlerFor('log:created')(log)

    expect(onLog).toHaveBeenCalledWith(log)
  })

  it('propaga el registro que llega por log:updated', () => {
    const onLog = vi.fn()
    mountSocket(onLog)
    const log = makeLog('2026-08-02')

    handlerFor('log:updated')(log)

    expect(onLog).toHaveBeenCalledWith(log)
  })

  it('al desmontar retira ambos manejadores y detiene la conexión', () => {
    mountSocket(vi.fn())
    const created = handlerFor('log:created')
    const updated = handlerFor('log:updated')

    wrapper!.unmount()

    expect(connection.off).toHaveBeenCalledWith('log:created', created)
    expect(connection.off).toHaveBeenCalledWith('log:updated', updated)
    expect(connection.stop).toHaveBeenCalledTimes(1)
  })
})
