# E2E

Pruebas end-to-end del Mental Health Tracker con Playwright y TypeScript.

## Estructura

```
e2e/
├── tests/               # Suites de pruebas
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

Para ver el reporte HTML: `npm run report`.
