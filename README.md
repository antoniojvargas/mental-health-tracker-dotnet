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
| Unitarias (frontend)  | Lógica de componentes y *composables* de Vue, sin navegador.                                                    | `cd frontend && npm run test:unit`                                                                                                            | Runner listo (5)   |
| E2E (Playwright)      | Flujos completos de usuario navegador → frontend → API → PostgreSQL.                                            | `make test-e2e` *(pendiente de configurar; requiere el stack levantado con `make up`)*                                                         | Pendiente           |

**Notas**

- `make test` fusiona la cobertura de las dos capas del backend (Coverlet + ReportGenerator) y **falla si la cobertura de líneas baja del 70 %**; la medición actual es de 75 %.
- `npm run test:unit` del frontend usa Vitest con `environment: 'node'` y `fetch` mockeado, así que corre sin navegador. Hoy cubre el cliente de la API; la lógica de componentes y *composables* sigue sin runner que la alcanzan, que es lo que persiguen los números de las otras capas.
- La capa E2E aún no tiene runner configurado: `e2e/` solo declara la estructura planificada. Cuando se configure, `make test-e2e` será el comando de entrada.