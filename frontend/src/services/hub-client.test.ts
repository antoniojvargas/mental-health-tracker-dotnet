import { beforeEach, describe, expect, it, vi } from 'vitest'

// El constructor del SDK se sustituye por un doble que registra cómo se configuró la
// conexión: aquí se prueba el singleton y la configuración, no SignalR.
const built: { id: number }[] = []
const withUrl = vi.fn()
const withAutomaticReconnect = vi.fn()
const build = vi.fn()
const start = vi.fn().mockResolvedValue(undefined)
const stop = vi.fn()
let builderCreated = 0

vi.mock('@microsoft/signalr', () => {
  class HubConnectionBuilder {
    constructor() {
      builderCreated += 1
    }

    withUrl(...args: unknown[]) {
      withUrl(...args)
      return this
    }

    withAutomaticReconnect(...args: unknown[]) {
      withAutomaticReconnect(...args)
      return this
    }

    build() {
      build()
      const connection = { id: built.length + 1, start, stop }
      built.push(connection)
      return connection
    }
  }

  return { HubConnectionBuilder }
})

async function loadModule() {
  vi.resetModules()
  return await import('./hub-client')
}

describe('hub-client', () => {
  beforeEach(() => {
    built.length = 0
    builderCreated = 0
    withUrl.mockClear()
    withAutomaticReconnect.mockClear()
    build.mockClear()
    start.mockClear()
    stop.mockClear()
  })

  it('importar el módulo no crea ninguna conexión', async () => {
    await loadModule()

    expect(builderCreated).toBe(0)
    expect(build).not.toHaveBeenCalled()
  })

  it('la primera llamada construye la conexión y no la arranca', async () => {
    const { getLogHubConnection } = await loadModule()

    const connection = getLogHubConnection()

    expect(build).toHaveBeenCalledTimes(1)
    expect(connection).toBe(built[0])
    expect(start).not.toHaveBeenCalled()
  })

  it('es un singleton: las llamadas siguientes devuelven la misma conexión', async () => {
    const { getLogHubConnection } = await loadModule()

    const first = getLogHubConnection()
    const second = getLogHubConnection()
    const third = getLogHubConnection()

    expect(second).toBe(first)
    expect(third).toBe(first)
    expect(build).toHaveBeenCalledTimes(1)
  })

  it('apunta a /hub/logs, sin prefijo /api, con credenciales para mandar la cookie', async () => {
    const { getLogHubConnection } = await loadModule()

    getLogHubConnection()

    expect(withUrl).toHaveBeenCalledTimes(1)
    expect(withUrl).toHaveBeenCalledWith('/hub/logs', { withCredentials: true })
  })

  it('activa la reconexión automática', async () => {
    const { getLogHubConnection } = await loadModule()

    getLogHubConnection()

    expect(withAutomaticReconnect).toHaveBeenCalledTimes(1)
  })

  it('cada carga nueva del módulo parte sin conexión (el estado vive en el módulo)', async () => {
    const first = (await loadModule()).getLogHubConnection()
    const second = (await loadModule()).getLogHubConnection()

    expect(second).not.toBe(first)
    expect(build).toHaveBeenCalledTimes(2)
  })

  describe('conteo de consumidores', () => {
    it('un solo consumidor: acquire arranca la conexión y release la detiene', async () => {
      const { acquireLogHub, releaseLogHub } = await loadModule()

      const { connection: hub } = acquireLogHub()

      expect(hub).toBe(built[0])
      expect(start).toHaveBeenCalledTimes(1)
      expect(stop).not.toHaveBeenCalled()

      releaseLogHub()

      expect(stop).toHaveBeenCalledTimes(1)
    })

    it('dos consumidores: arranca con el primero y se detiene solo con el último', async () => {
      const { acquireLogHub, releaseLogHub } = await loadModule()

      const first = acquireLogHub()
      const second = acquireLogHub()

      expect(second.connection).toBe(first.connection)
      expect(second.started).toBe(first.started)
      expect(build).toHaveBeenCalledTimes(1)
      expect(start).toHaveBeenCalledTimes(1)

      releaseLogHub()
      expect(stop).not.toHaveBeenCalled()

      releaseLogHub()
      expect(stop).toHaveBeenCalledTimes(1)
    })

    it('si start() falla, started se rechaza y no se reintenta', async () => {
      start.mockRejectedValueOnce(new Error('servidor caído'))
      const { acquireLogHub } = await loadModule()

      const { started } = acquireLogHub()

      await expect(started).rejects.toThrow('servidor caído')
      expect(start).toHaveBeenCalledTimes(1)
    })

    it('tras detenerse, un consumidor nuevo vuelve a arrancarla', async () => {
      const { acquireLogHub, releaseLogHub } = await loadModule()

      acquireLogHub()
      releaseLogHub()
      acquireLogHub()

      expect(start).toHaveBeenCalledTimes(2)
    })

    it('soltar de más no detiene ni descuenta a los consumidores vivos', async () => {
      const { acquireLogHub, releaseLogHub } = await loadModule()

      releaseLogHub()
      expect(stop).not.toHaveBeenCalled()

      acquireLogHub()
      releaseLogHub()
      releaseLogHub()

      expect(stop).toHaveBeenCalledTimes(1)
    })
  })
})
