/**
 * Contrato del hub de registros con el backend: la ruta y los nombres de evento.
 *
 * Espejo de `HubRoutes` y `LogHubEvents` (backend/MentalHealthTracker.Api/Modules/Realtime).
 * Cambiar cualquiera de estos valores rompe la comunicación, así que hub-events.test.ts
 * los fija como literales.
 *
 * La ruta va relativa al origen, sin el prefijo `/api`: el proxy de Vite y el de
 * producción (nginx) reenvían `/hub` contra el backend.
 */

export const LOGS_HUB_PATH = '/hub/logs'

export const LOG_HUB_EVENTS = {
  created: 'log:created',
  updated: 'log:updated',
} as const
