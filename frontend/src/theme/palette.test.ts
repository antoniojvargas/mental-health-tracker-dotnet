import { describe, expect, it } from 'vitest'

import tailwindConfig from '../../tailwind.config'
import { METRICS } from '../components/charts/metrics'
import { palette } from './palette'

// Los hex de la paleta, tal como estaban escritos antes de centralizarla. Si cambia un tono a
// propósito, se cambia aquí y en palette.ts: el test existe para que no cambie sin querer.
describe('palette', () => {
  it('conserva los hex de cada token', () => {
    expect(palette).toEqual({
      paper: { 50: '#FBF8F2', 100: '#F6F1E7', 200: '#EAE0CB', 300: '#D7C7A2', 400: '#BFA679' },
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
      clearsky: {
        50: '#EDF3F5',
        100: '#D7E6EA',
        200: '#B4CFD7',
        300: '#96BAC5',
        400: '#7FA3B2',
        500: '#6F94A6',
        600: '#587989',
        700: '#465F6C',
        800: '#3A4F5A',
      },
      meadow: {
        50: '#EFF3EC',
        100: '#DCE6D6',
        200: '#BFD3B4',
        300: '#A3BE95',
        400: '#93AA89',
        500: '#7C9473',
        600: '#63775B',
      },
      ember: { 100: '#F3DCC9', 300: '#E7BBA0', 400: '#D89A78', 500: '#C97C5D' },
    })
  })

  it('Tailwind recibe exactamente esta paleta', () => {
    expect(tailwindConfig.theme.extend.colors).toBe(palette)
  })

  it('las series de la gráfica usan los mismos colores de siempre', () => {
    expect(Object.fromEntries(METRICS.map((m) => [m.key, m.color]))).toEqual({
      mood: '#7C9473',
      anxiety: '#6F94A6',
      stress: '#C97C5D',
      sleepHours: '#465F6C',
      sleepQuality: '#A3BE95',
      activityMinutes: '#63775B',
      symptomLoad: '#33453F',
    })
  })
})
