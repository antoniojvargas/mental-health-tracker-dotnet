import { describe, expect, it } from 'vitest'

import { LOG_HUB_EVENTS, LOGS_HUB_PATH } from './hub-events'

// Son cadenas del contrato con el backend (HubRoutes.Logs y LogHubEvents): el test las fija
// con literales para que un cambio accidental aquí no pase desapercibido.
describe('hub-events', () => {
  it('apunta a la ruta del hub que sirve el backend', () => {
    expect(LOGS_HUB_PATH).toBe('/hub/logs')
  })

  it('usa los nombres de evento que emite el backend', () => {
    expect(LOG_HUB_EVENTS).toEqual({ created: 'log:created', updated: 'log:updated' })
  })
})
