import type { Config } from 'tailwindcss'

export default {
  theme: {
    extend: {
      colors: {
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
      },
      fontFamily: {
        // Default everywhere (`font-sans`, and the body via preflight). A calm
        // text serif, not a UI grotesque: reading, not interface chrome. All
        // body copy, labels, form text and buttons stay here.
        sans: ['"Newsreader"', 'Georgia', 'ui-serif', 'serif'],
        // Sparingly, for hero moments and section titles only. It is a display
        // face, so long runs of it are wrong — never paragraphs or dense UI.
        display: ['"Bricolage Grotesque"', 'system-ui', 'sans-serif'],
        // Dates, figures, chart data and any value that must align in a column
        // — the logbook texture. It is the only family with tabular figures.
        mono: ['"IBM Plex Mono"', 'ui-monospace', 'monospace'],
      },
    },
  },
} satisfies Config
