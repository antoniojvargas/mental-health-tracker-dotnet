# Mental Health Tracker

Mental Health Tracker es una aplicación diseñada para que pacientes registren su estado de salud mental de forma diaria. A través de un flujo sencillo de registro, cada paciente puede anotar su ánimo, energía, sueño y otros factores relevantes en una captura rápida diaria, construyendo así un historial personal persistente y confiable.

Con base en esos registros, la aplicación genera tendencias semanales y mensuales que permiten al paciente (y a su profesional de la salud) visualizar la evolución de su estado a lo largo del tiempo. Las tendencias se presentan de forma clara e intuitiva, facilitando la detección de patrones y el acompañamiento en el proceso terapéutico.

## Stack

| Tecnología   | Versión       | Uso                              |
| ------------ | ------------- | -------------------------------- |
| .NET         | 9             | Runtime del backend              |
| ASP.NET Core | 9             | API y servicios web              |
| EF Core      | 9             | ORM y acceso a datos             |
| PostgreSQL   | 16            | Base de datos                    |
| Vue 3        | 3             | Interfaz de usuario              |
| Vite         | —             | Build tooling del frontend       |
| Tailwind     | —             | Estilos y diseño                 |
| SignalR      | —             | Comunicación en tiempo real      |
| Docker       | —             | Contenedores y despliegue local  |

## Arranque con Docker

Para levantar el entorno de desarrollo completo:

```bash
cp .env.example .env
docker compose up --build
```

El stack levanta tres servicios con hot-reload (los cambios en el código se aplican sin reconstruir la imagen).

### Puertos expuestos

| Servicio  | Puerto            | Descripción                    |
| --------- | ----------------- | ------------------------------ |
| Frontend  | `5173`            | App Vue 3 + Vite (dev server)  |
| API       | `3000`            | Backend .NET (dotnet watch)    |
| Postgres  | `5432`            | Base de datos PostgreSQL 16    |

### Documentación de la API

Disponible en desarrollo en `http://localhost:3000/openapi/v1.json` (OpenAPI generado por ASP.NET Core).

## Testing

### Capas de pruebas

| Capa                  | Qué cubre                                                                                                       | Cómo ejecutarla                                                                                                                              | Estado              |
| --------------------- | --------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- | ------------------- |
| Unitarias (backend)   | Lógica de dominio, validación y casos límite de la API, aislada de la base de datos y de HTTP.                  | `dotnet test backend/tests/MentalHealthTracker.UnitTests/MentalHealthTracker.UnitTests.csproj`                                                | Configurado (52)    |
| Integración (backend) | Endpoints HTTP, autenticación OAuth, repositorios y SignalR contra una PostgreSQL 16 real (Docker, puerto `5433`). | `make test` — levanta `postgres-test`, ejecuta las dos capas del backend y verifica el umbral de cobertura                                 | Configurado (19)    |
| Unitarias (frontend)  | Lógica de componentes y *composables* de Vue, sin navegador.                                                    | `cd frontend && npm run test:unit`                                                                                                            | Runner listo (22)  |
| E2E (Playwright)      | Flujos completos de usuario navegador → frontend → API → PostgreSQL.                                            | Requiere la pila E2E arriba; ver [Pruebas E2E](#pruebas-e2e-playwright)                                                                       | Configurado (5)     |

**Notas**

- `make test` fusiona la cobertura de las dos capas del backend (Coverlet + ReportGenerator) y **falla si la cobertura de líneas baja del 70 %**; la medición actual es de 75 %.
- `npm run test:unit` del frontend usa Vitest y corre sin navegador. El entorno global es `node`, porque el cliente de la API y el store no tocan el DOM; los tests de componente que sí lo necesitan montan la vista con Vue Test Utils sobre `jsdom` mediante un docblock `@vitest-environment`, de modo que solo ese fichero paga el coste de levantar un DOM.
- La suite de frontend cubre hoy el cliente de la API, el store de autenticación y la vista de login. Los tests de componente montan la vista real con un router de memoria y un Pinia reales, y solo sustituyen `fetch`: la lógica que interesa es la que une el `query` de la ruta, el store y el router, así que falsificar cualquiera de los dos dejaría el cableado real fuera del alcance del test.
- La suite E2E vive en `e2e/`; `make test-e2e` ejecuta la suite completa y requiere la pila E2E levantada. Ver [Pruebas E2E](#pruebas-e2e-playwright) para levantar la pila, instalar los navegadores de Playwright y volver al entorno normal.

### Pruebas E2E (Playwright)

La suite E2E vive en `e2e/` (Playwright + TypeScript, solo Chromium) y recorre flujos completos de usuario contra el stack real: navegador → frontend → API → PostgreSQL. El `baseURL` es `http://localhost:5173`, así que primero hay que tener una pila viva.

1. **Levantar la pila E2E.** Primero crea un `.env` desde `.env.example` con valores desechables: el override `docker-compose.e2e.yml` lo inyecta al backend dentro de Docker. Se arranca con el override, que fija `ASPNETCORE_ENVIRONMENT=Testing` en el backend y habilita `POST /api/auth/test-login`, el endpoint que las pruebas usan para autenticarse sin pasar por el consentimiento de Google:

   ```bash
   docker compose -f docker-compose.yml -f docker-compose.e2e.yml up -d --build
   ```

2. **Instalar los navegadores de Playwright** (solo la primera vez):

   ```bash
   cd e2e && npm install && npx playwright install chromium
   ```

3. **Ejecutar la suite**, ya sea completa o solo los tests etiquetados `@smoke`:

   ```bash
   cd e2e && npm test            # suite completa (smoke + flujos críticos)
   cd e2e && npm run test:smoke  # solo los tests con la etiqueta @smoke
   ```

   Tras la ejecución, el reporte HTML queda en `e2e/playwright-report/`; se abre con `npm run report`.

4. **Volver a la pila normal.** El override solo aplica al entorno de pruebas, así que hay que bajar la pila E2E y levantarla sin él (equivalente a `make up`) para que el backend deje de correr con `ASPNETCORE_ENVIRONMENT=Testing` y `POST /api/auth/test-login` deje de existir:

   ```bash
   docker compose down
   docker compose up -d --build
   ```