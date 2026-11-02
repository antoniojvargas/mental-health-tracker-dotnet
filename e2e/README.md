# E2E

Pruebas end-to-end del Mental Health Tracker con Playwright y TypeScript.

## Estructura

```
e2e/
├── tests/               # Specs (ver la tabla de abajo)
├── fixtures/            # csp.ts: hace fallar cualquier test ante una violación de CSP
├── playwright.config.ts # Configuración de Playwright (proyectos chromium y prod-smoke)
├── prod-secrets.sh      # Secretos desechables para prod-smoke (a e2e/prod-secrets/, ignorado por git)
├── prod-smoke.env       # Variables no sensibles de la pila de producción de prueba
├── tsconfig.json        # Configuración de TypeScript
└── package.json         # Dependencias y scripts
```

## Specs

| Spec | Qué recorre |
| --- | --- |
| `smoke.spec.ts` (`@smoke`) | La página de login con el enlace de Google, `GET /api/health` y que el endpoint del hub responda algo distinto de un 5xx. Lo más barato para saber que la pila está viva: `npm run test:smoke`. |
| `critical-flow.spec.ts` | **Recorrido crítico:** `POST /api/auth/test-login`, dashboard, registrar el ánimo y confirmar el guardado **sin recargar** la página. Incluye el caso de **zona horaria** (abajo). |
| `keyboard.spec.ts` | **Solo teclado**, de login a tendencias: Tab/Shift+Tab, flechas, Espacio y Enter por el login, el modal y sus cuatro pasos, el guardado y el panel (rango y métricas). Además, que los chips de métricas deshabilitados se salten con Tab (WCAG 2.1.1). |
| `realtime.spec.ts` | **Tiempo real:** con dos pestañas del mismo usuario, guardar en una se refleja en la otra por el websocket del hub, sin recargar y sin volver a pedir `GET /api/logs`. Espera el handshake de SignalR para no perder la emisión. |
| `csp-fixture.spec.ts` | Comprueba el propio fixture de CSP: con una política que bloquea un script, el test debe fallar (`test.fail()`), y sin violaciones no interfiere. |

### Zona horaria

Dentro de `critical-flow.spec.ts`, el bloque «día local en America/Bogota» fija `timezoneId` y un reloj
simulado a las 20:00 locales del 15 de marzo (01:00 UTC del 16). Guarda, cierra y reabre el modal, y
comprueba que los valores siguen ahí y que todas las consultas de «hoy» (`GET /api/logs/today?date=`)
llevaron el día **local** (`2026-03-15`) y no el UTC. Es la prueba de extremo a extremo de que el cliente
decide el día ([ADR 0001](../docs/adr/0001-el-cliente-decide-el-dia.md)).

## Configuración

- **Proyecto `chromium`:** todas las specs contra el dev server de Vite, `http://localhost:5173`.
- **Proyecto `prod-smoke`:** solo `critical-flow.spec.ts` contra la imagen de producción, `http://localhost:8080`.
- **Navegador:** Chromium.
- **Reintentos:** 2 en CI, 0 en local.
- **Reporte:** HTML (se genera en `playwright-report/`).

## Uso

Primero hace falta una pila viva. La suite `chromium` necesita la **pila E2E**: el stack de desarrollo con
el override `docker-compose.e2e.yml`, que fija `ASPNETCORE_ENVIRONMENT=Testing` y habilita
`POST /api/auth/test-login` para entrar sin pasar por Google. Los pasos (un `.env` desechable, el
`docker compose … up` y cómo volver a la pila normal) están en el
[README de la raíz](../README.md#pruebas-e2e-playwright).

```sh
npm install
npx playwright install chromium
npm test                # proyecto chromium
npm run test:smoke      # solo los @smoke
```

Desde la raíz del repositorio, con la pila E2E ya arriba:

```sh
make test-e2e           # equivale a `cd e2e && npm test` (proyecto chromium)
```

### prod-smoke: la imagen real de producción

El proyecto `prod-smoke` ejecuta `critical-flow.spec.ts` (el recorrido crítico y el caso de zona horaria)
contra las imágenes de producción (nginx + bundle compilado) en `http://localhost:8080`, que es donde se
ejerce la CSP y las cabeceras de nginx. Desde la raíz del repositorio es un solo comando:

```sh
make smoke-prod
```

Genera los secretos desechables, construye y levanta la pila de producción de prueba (proyecto
`mht-prod-e2e`), ejecuta `npm run test:prod-smoke` y **baja la pila siempre**, también si el recorrido
falla (el código de salida es el del recorrido). Es lo mismo que hace el job `Prod smoke` del CI. A mano:

```sh
./prod-secrets.sh          # secretos desechables en e2e/prod-secrets/ (ignorado por git)
docker compose -p mht-prod-e2e --env-file prod-smoke.env \
  -f ../docker-compose.prod.yml -f ../docker-compose.prod.e2e.yml up -d --build --wait
npm run test:prod-smoke
docker compose -p mht-prod-e2e --env-file prod-smoke.env \
  -f ../docker-compose.prod.yml -f ../docker-compose.prod.e2e.yml down -v
```

Los tests importan `test` de `fixtures/csp.ts`, que hace fallar cualquier test si la
consola informa de una violación de CSP. La pila comparte la subred `172.28.0.0/24` con
`docker-compose.prod.yml`: no pueden estar arriba las dos a la vez.

Para ver el reporte HTML: `npm run report`.
