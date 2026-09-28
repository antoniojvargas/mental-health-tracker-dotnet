# Frontend

Vue 3 + TypeScript + Vite. Tailwind CSS 4 se configura con
`tailwind.config.ts`, cargado desde `src/index.css` vía `@config`.

## Comandos

| Script                   | Qué hace                                       |
| ------------------------ | ---------------------------------------------- |
| `npm run lint`           | ESLint sobre el proyecto, sin escribir cambios |
| `npm run lint:fix`       | ESLint corrigiendo lo que se puede             |
| `npm run format`         | Comprueba el formato con Prettier              |
| `npm run format:fix`     | Aplica el formato                              |
| `npm run generate:types` | Regenera los tipos desde el snapshot OpenAPI   |

`api.generated.d.ts` y `openapi.json` están fuera del lint y del formato:
los dos los genera una herramienta y no son código nuestro.

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

Definida en `tailwind.config.ts`. `ember` está reservada para el degradado
ambiental del login y no debe usarse en el cromo de la interfaz.

## Tipos del contrato

`src/types/daily-log.ts` es el único sitio donde el frontend describe el
contrato con la API. Si necesitas la forma de un campo que no esté ahí,
no la redeclares en el componente: añádela al archivo.

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
