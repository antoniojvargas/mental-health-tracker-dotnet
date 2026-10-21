import type { Config } from 'tailwindcss'
import plugin from 'tailwindcss/plugin'

import { palette } from './src/theme/palette.ts'

// Tailwind 4 solo escribe las variables `--color-*` de los colores que vienen de `@theme` en CSS, no
// de esta config en JS. Las genera este plugin desde la misma paleta, con el mismo nombre que usa
// Tailwind (`--color-ink-200`), para que index.css pueda usar `var(--color-ink-200)` sin repetir el hex.
const themeColorVariables = plugin(({ addBase }) => {
  const variables: Record<string, string> = {}
  for (const [name, tones] of Object.entries(palette)) {
    for (const [tone, hex] of Object.entries(tones)) {
      variables[`--color-${name}-${tone}`] = hex
    }
  }
  addBase({ ':root': variables })
})

export default {
  theme: {
    extend: {
      colors: palette,
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
      keyframes: {
        'fade-in': { from: { opacity: '0' }, to: { opacity: '1' } },
        'slide-up': {
          from: { opacity: '0', transform: 'translateY(8px)' },
          to: { opacity: '1', transform: 'translateY(0)' },
        },
      },
      // Durations stay in the 200-250ms band and ease out: motion should
      // settle the interface, never draw attention to itself.
      animation: {
        'fade-in': 'fade-in 200ms ease-out',
        'slide-up': 'slide-up 250ms ease-out',
      },
    },
  },
  plugins: [themeColorVariables],
} satisfies Config
