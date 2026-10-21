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
import { acquireLogHub, releaseLogHub } from '../../services/hub-client'
import type { DailyLog } from '../../types/daily-log'
import { makeLog } from '../../test-support/factories'

vi.mock('../../services/hub-client', () => ({
  acquireLogHub: vi.fn(),
  releaseLogHub: vi.fn(),
}))

type ConnectionHandler = (log: DailyLog) => void

const connection = {
  on: vi.fn<(event: string, handler: ConnectionHandler) => void>(),
  off: vi.fn<(event: string, handler: ConnectionHandler) => void>(),
  onreconnected: vi.fn<(callback: () => void) => void>(),
  start: vi.fn<() => Promise<void>>().mockResolvedValue(undefined),
  stop: vi.fn<() => Promise<void>>().mockResolvedValue(undefined),
}

let wrapper: VueWrapper | null = null

function mountSocket(onLog: ConnectionHandler, onReconnected?: () => void): VueWrapper {
  const Harness = defineComponent({
    setup() {
      useLogSocket(onLog, onReconnected)
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
    vi.mocked(acquireLogHub).mockReturnValue({
      connection: connection as never,
      started: Promise.resolve(),
    })
    vi.mocked(acquireLogHub).mockClear()
    vi.mocked(releaseLogHub).mockClear()
    connection.on.mockClear()
    connection.off.mockClear()
    connection.onreconnected.mockClear()
    connection.start.mockClear()
    connection.stop.mockClear()
  })

  afterEach(() => {
    wrapper?.unmount()
    wrapper = null
  })

  it('al montar pide la conexión y se suscribe a los dos eventos', () => {
    mountSocket(vi.fn())

    expect(acquireLogHub).toHaveBeenCalledTimes(1)
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
    const log = makeLog({ logDate: '2026-08-01' })

    handlerFor('log:created')(log)

    expect(onLog).toHaveBeenCalledWith(log)
  })

  it('propaga el registro que llega por log:updated', () => {
    const onLog = vi.fn()
    mountSocket(onLog)
    const log = makeLog({ logDate: '2026-08-02' })

    handlerFor('log:updated')(log)

    expect(onLog).toHaveBeenCalledWith(log)
  })

  it('al desmontar retira ambos manejadores y suelta la conexión', () => {
    mountSocket(vi.fn())
    const created = handlerFor('log:created')
    const updated = handlerFor('log:updated')

    wrapper!.unmount()

    expect(connection.off).toHaveBeenCalledWith('log:created', created)
    expect(connection.off).toHaveBeenCalledWith('log:updated', updated)
    expect(releaseLogHub).toHaveBeenCalledTimes(1)
  })

  it('al reconectar avisa para que el cliente recupere lo perdido durante el corte', () => {
    const onReconnected = vi.fn()
    mountSocket(vi.fn(), onReconnected)

    expect(connection.onreconnected).toHaveBeenCalledTimes(1)
    expect(onReconnected).not.toHaveBeenCalled()

    connection.onreconnected.mock.calls[0][0]()

    expect(onReconnected).toHaveBeenCalledTimes(1)
  })

  it('tras desmontar, una reconexión ya no avisa', () => {
    const onReconnected = vi.fn()
    mountSocket(vi.fn(), onReconnected)
    const callback = connection.onreconnected.mock.calls[0][0]

    wrapper!.unmount()
    callback()

    expect(onReconnected).not.toHaveBeenCalled()
  })

  it('sin callback de reconexión no registra nada', () => {
    mountSocket(vi.fn())

    expect(connection.onreconnected).not.toHaveBeenCalled()
  })

  it('con dos componentes, desmontar uno retira solo sus manejadores y suelta una vez', () => {
    const first = vi.fn()
    const second = vi.fn()
    const firstWrapper = mountSocket(first)
    const firstHandler = handlerFor('log:created')
    mountSocket(second)
    const secondHandler = connection.on.mock.calls.filter(([name]) => name === 'log:created')[1][1]

    firstWrapper.unmount()

    expect(acquireLogHub).toHaveBeenCalledTimes(2)
    expect(releaseLogHub).toHaveBeenCalledTimes(1)
    expect(connection.off).toHaveBeenCalledWith('log:created', firstHandler)
    expect(connection.off).not.toHaveBeenCalledWith('log:created', secondHandler)

    const log = makeLog({ logDate: '2026-08-03' })
    secondHandler(log)
    expect(second).toHaveBeenCalledWith(log)
  })

  it('si el arranque falla lo registra con console.warn, sin reintentar ni dejar un rechazo sin manejar', async () => {
    const failure = new Error('servidor caído')
    vi.mocked(acquireLogHub).mockReturnValue({
      connection: connection as never,
      started: Promise.reject(failure),
    })
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined)

    mountSocket(vi.fn())
    await Promise.resolve()
    await Promise.resolve()

    expect(warn).toHaveBeenCalledTimes(1)
    expect(warn).toHaveBeenCalledWith(expect.any(String), failure)
    expect(acquireLogHub).toHaveBeenCalledTimes(1)
    warn.mockRestore()
  })

  it('si el arranque va bien no escribe ningún aviso', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined)

    mountSocket(vi.fn())
    await Promise.resolve()
    await Promise.resolve()

    expect(warn).not.toHaveBeenCalled()
    warn.mockRestore()
  })
})
