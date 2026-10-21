/**
 * Paleta de la aplicación: la única fuente de los colores.
 *
 * La consumen tailwind.config.ts (las utilidades `bg-paper-50`, `text-ink-700`…), las constantes
 * de la gráfica (components/charts/TrendChart.vue), los colores de las series (metrics.ts) y, por
 * las variables CSS de tema de Tailwind, index.css. Cambiar un tono aquí lo cambia en todos.
 * Los colores se nombran por su token (`palette.ink[700]`), no por su hex.
 */

export const palette = {
  // Warm linen paper — page and card backgrounds.
  paper: {
    50: '#FBF8F2',
    100: '#F6F1E7',
    200: '#EAE0CB',
    300: '#D7C7A2',
    400: '#BFA679',
  },
  // Teal-charcoal "ink" — all text and hairline borders.
  ink: {
    50: '#EEF2F1',
    100: '#DCE4E2',
    200: '#B9C9C6',
    300: '#8A9B97',
    400: '#5D726D',
    500: '#3E524E',
    600: '#33453F',
    700: '#2B3A3A',
  },
  // Dusty blue — the primary interactive accent (buttons, focus, active states).
  clearsky: {
    50: '#EDF3F5',
    100: '#D7E6EA',
    200: '#B4CFD7',
    300: '#96BAC5',
    400: '#7FA3B2',
    500: '#6F94A6',
    600: '#587989',
    700: '#465F6C',
    // Un escalón más oscuro que 700, solo para el `hover` del botón
    // primario: con 700 como relleno base (necesario para que el texto
    // `paper-50` llegue a 4.5:1) hacía falta un tono de hover más oscuro
    // que siguiera cumpliendo el contraste, y no existía.
    800: '#3A4F5A',
  },
  // Soft sage — secondary/confirmation accent, used sparingly.
  meadow: {
    50: '#EFF3EC',
    100: '#DCE6D6',
    200: '#BFD3B4',
    300: '#A3BE95',
    400: '#93AA89',
    500: '#7C9473',
    600: '#63775B',
  },
  // Warm clay — reserved for the ambient login gradient only, never UI chrome.
  ember: {
    100: '#F3DCC9',
    300: '#E7BBA0',
    400: '#D89A78',
    500: '#C97C5D',
  },
} as const

export type PaletteName = keyof typeof palette
