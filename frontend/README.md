# Frontend

Vue 3 + TypeScript + Vite. Tailwind CSS 4 se configura con
`tailwind.config.ts`, cargado desde `src/index.css` vía `@config`.

## Comandos

| Script                       | Qué hace                                                                     |
| ---------------------------- | ---------------------------------------------------------------------------- |
| `npm run dev`                | Dev server de Vite en `http://localhost:5173` (proxy de `/api` y `/hub`)     |
| `npm run build`              | Comprueba los tipos (`vue-tsc -b`) y genera el bundle                        |
| `npm run typecheck`          | Solo los tipos, sin generar nada                                             |
| `npm run test:unit`          | Vitest, una pasada                                                           |
| `npm run test:unit:coverage` | Vitest con cobertura; falla por debajo de los umbrales de `vitest.config.ts` |
| `npm run lint`               | ESLint sobre el proyecto, sin escribir cambios                               |
| `npm run lint:fix`           | ESLint corrigiendo lo que se puede                                           |
| `npm run format`             | Comprueba el formato con Prettier                                            |
| `npm run format:fix`         | Aplica el formato                                                            |
| `npm run generate:types`     | Regenera los tipos desde el snapshot OpenAPI                                 |

Desde la raíz, `make test-frontend` encadena la comprobación de tipos generados, el build y
`test:unit:coverage` (como el job `Frontend tests` del CI) y `make lint` incluye ESLint y Prettier.

`api.generated.d.ts` y `openapi.json` están fuera del lint y del formato:
los dos los genera una herramienta y no son código nuestro.

## Estructura de `src/`

| Carpeta            | Qué hay                                                                                                                                                                                                    |
| ------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `views/`           | Las dos pantallas: `LoginView` y `DashboardView`.                                                                                                                                                          |
| `components/`      | `ui/` (Button, Modal, Toast, Tooltip…), `charts/` (gráfica de tendencias y sus selectores) y `daily-log/` (el modal de registro, sus cuatro pasos en `steps/` y los módulos de opciones y mapeo de abajo). |
| `composables/`     | `useDailyLogForm` (estado y pasos del formulario), `useLogSocket` (eventos en tiempo real) y `useToast`.                                                                                                   |
| `stores/`          | Pinia: `useAuthStore` (sesión) y `useLogsStore` (registros de la ventana activa).                                                                                                                          |
| `services/`        | Todo lo que habla con el backend: `api-client` y sus módulos (`auth.api`, `logs.api`), `error-messages`, y el hub de SignalR (`hub-client`, `hub-events`).                                                 |
| `types/`           | El contrato con la API y los tipos de la interfaz (ver «Tipos del contrato»).                                                                                                                              |
| `utils/`, `theme/` | `date.ts` y `palette.ts`, descritos abajo.                                                                                                                                                                 |
| `test-support/`    | Utilidades de los tests (`http.ts` para falsear `fetch`, `factories.ts` con `makeLog` y `makeUser`); las typechequea `vue-tsc` como el resto.                                                              |

Cada concepto tiene **una sola fuente**; si lo necesitas, impórtalo, no lo vuelvas a escribir:

| Módulo                                      | Qué fija                                                                                                                                                                                                                                                                                                                                                                                                                        | Quién lo usa                                                                                                                                                                                                                                         |
| ------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `utils/date.ts`                             | El «hoy» y las fechas del cliente, siempre en **hora local**: `todayIso()`, `toIsoDate(date)` y `parseIsoDate(iso)`. El servidor acepta el día que envía el cliente ([ADR 0001](../docs/adr/0001-el-cliente-decide-el-dia.md)).                                                                                                                                                                                                 | El guardado (`logDate`), la consulta de hoy (`?date=`), la ventana del panel y la gráfica.                                                                                                                                                           |
| `theme/palette.ts`                          | La paleta completa, por token (`palette.ink[700]`), no por hex.                                                                                                                                                                                                                                                                                                                                                                 | `tailwind.config.ts` (las utilidades `bg-paper-50`…), las constantes de `TrendChart.vue`, los colores de las series (`metrics.ts`) y `index.css`, que usa las variables `--color-<tono>-<n>` que un plugin de Tailwind genera desde la misma paleta. |
| `components/daily-log/daily-log-options.ts` | Las opciones y etiquetas de los selectores del formulario, **1:1 con los enums** del contrato: `SLEEP_DISTURBANCE_OPTIONS`, `ACTIVITY_TYPE_OPTIONS`, `SOCIAL_FREQUENCY_OPTIONS`, `SYMPTOM_OPTIONS` y `createDefaultSymptoms()`. Cada tabla es un `Record<Tipo, string>`: si el backend añade un valor, TypeScript falla hasta que tenga etiqueta ([ADR 0004](../docs/adr/0004-opciones-del-formulario-1-a-1-con-los-enums.md)). | `steps/*`, `SymptomPicker`, `useDailyLogForm`.                                                                                                                                                                                                       |
| `components/daily-log/daily-log-mapper.ts`  | La conversión pura, sin pérdidas, entre el formulario y la API: `toCreateDailyLogInput(form, today)` y `fromDailyLog(log)`.                                                                                                                                                                                                                                                                                                     | `DailyLogModal` (guardar y reabrir). Su test comprueba la ida y vuelta de cada valor de cada enum.                                                                                                                                                   |
| `services/hub-events.ts`                    | La ruta del hub (`LOGS_HUB_PATH`) y los nombres de evento (`LOG_HUB_EVENTS`), espejo de `HubRoutes` y `LogHubEvents` del backend. Un test los fija como literales.                                                                                                                                                                                                                                                              | `hub-client.ts` y `useLogSocket.ts`.                                                                                                                                                                                                                 |

Reglas de uso:

- **Las fechas no se formatean a mano.** Una regla de ESLint (`no-restricted-syntax`) avisa de
  `getFullYear()` con `padStart()` fuera de `utils/date.ts`: usa `toIsoDate()` o `todayIso()`.
  `toISOString()` convierte a UTC y desplaza el día.
- **Los colores salen de `palette.ts`.** No escribas un hex en un componente; si falta un tono, añádelo
  a la paleta.
- **La conexión con el hub es compartida y contada.** `hub-client.ts` expone `acquireLogHub()` y
  `releaseLogHub()`: arranca con el primer consumidor y se detiene con el último, así que dos
  componentes que escuchan no abren dos sockets.

## Tipografía

Tres familias, tres usos y ningún solapamiento. Se cargan como webfonts desde
Google Fonts en `index.html`; los pesos disponibles son los que pides ahí, así
que **no inventes un peso que no esté en el `link`**.

| Familia             | Token          | Uso                                                                       | Pesos                      |
| ------------------- | -------------- | ------------------------------------------------------------------------- | -------------------------- |
| Newsreader          | `font-sans`    | Default. Todo el texto corrido, etiquetas, formularios y botones.         | 400, 500, 600, itálica 400 |
| Bricolage Grotesque | `font-display` | Títulos de sección y momentos protagonistas.                              | 500, 600, 700              |
| IBM Plex Mono       | `font-mono`    | Fechas, cifras, datos de gráfica y todo lo que deba alinearse en columna. | 400, 500                   |

`font-sans` es el default del body por el preflight de Tailwind, así que en texto
corrido no hace falta escribir la utilidad.

Dos reglas para no romper la intención del diseño:

- `font-display` es una display face. Es correcta en un `h1` protagonista y
  equivocada en un párrafo o en un bloque denso de interfaz: no la extiendas.
- `font-mono` es la única familia con cifras tabulares. Úsala siempre que un
  valor tenga que alinearse en vertical — columnas de la gráfica, listados de
  fechas, contadores.

## Paleta

Definida en `src/theme/palette.ts`, que es la única fuente (ver «Estructura de `src/`»):
`tailwind.config.ts`, la gráfica y `index.css` la leen de ahí. Un tono se cambia en un solo sitio.
`ember` está reservada para el degradado ambiental del login y no debe usarse en el cromo de la
interfaz.

## Tipos del contrato

El frontend describe el contrato con la API en dos archivos de `src/types/`, y solo ahí:

- `daily-log.ts`: los registros, los enums y los cuerpos de petición y respuesta.
- `api-error.ts`: el sobre de error (`ApiErrorBody`, `ApiErrorCode`, `ApiErrorDetail`,
  `ApiErrorResponse`).

Si necesitas la forma de un campo que no esté ahí, no la redeclares en el componente: añádela al archivo
que corresponda. `daily-log-form.ts` no es contrato: son los tipos del estado del formulario (`DailyLogForm`,
`SymptomEntry`), que `daily-log-mapper.ts` convierte hacia y desde la API.

`npm run generate:types` regenera `src/types/api.generated.d.ts` a partir
del snapshot `openapi.json`. **Ese archivo no se importa**: existe para
auditar que el espejo a mano no se desvía del backend. El motivo está
detallado en `docs/architecture.md`.

Para refrescar el snapshot hay que volcar el documento de una API en
Development, y `Program.cs` ejecuta las migraciones al arrancar, así que
hace falta Postgres:

```bash
docker compose up -d postgres
# arrancar la API con ASPNETCORE_ENVIRONMENT=Development
curl -s http://localhost:3000/swagger/v1/swagger.json > frontend/openapi.json
cd frontend && npm run generate:types
```
