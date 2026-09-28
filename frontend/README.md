# Frontend

Vue 3 + TypeScript + Vite. Tailwind CSS 4 se configura con
`tailwind.config.ts`, cargado desde `src/index.css` vía `@config`.

## Tipografía

Tres familias, tres usos y ningún solapamiento. Se cargan como webfonts desde
Google Fonts en `index.html`; los pesos disponibles son los que pides ahí, así
que **no inventes un peso que no esté en el `link`**.

| Familia              | Token       | Uso                                                                  | Pesos              |
| -------------------- | ----------- | -------------------------------------------------------------------- | ------------------ |
| Newsreader           | `font-sans` | Default. Todo el texto corrido, etiquetas, formularios y botones.     | 400, 500, 600, itálica 400 |
| Bricolage Grotesque  | `font-display` | Títulos de sección y momentos protagonistas.                        | 500, 600, 700      |
| IBM Plex Mono        | `font-mono` | Fechas, cifras, datos de gráfica y todo lo que deba alinearse en columna. | 400, 500       |

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
