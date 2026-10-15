# E2E

Pruebas end-to-end del Mental Health Tracker con Playwright y TypeScript.

## Estructura

```
e2e/
├── tests/               # Suites de pruebas
├── fixtures/            # Fixtures compartidos (fallo ante violaciones de CSP)
├── playwright.config.ts # Configuración de Playwright
├── tsconfig.json        # Configuración de TypeScript
└── package.json         # Dependencias y scripts
```

## Configuración

- **baseURL:** `http://localhost:5173` (dev server de Vite)
- **Navegador:** Chromium
- **Reintentos:** 2 en CI, 0 en local
- **Reporte:** HTML (se genera en `playwright-report/`)

## Uso

```sh
npm install
npx playwright install chromium
npm test
```

`npm test` corre el proyecto `chromium` contra el stack de desarrollo (Vite, puerto 5173).

### prod-smoke: la imagen real de producción

El proyecto `prod-smoke` ejecuta `critical-flow.spec.ts` contra las imágenes de
producción (nginx + bundle compilado) en `http://localhost:8080`, que es donde se
ejerce la CSP y las cabeceras de nginx:

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
