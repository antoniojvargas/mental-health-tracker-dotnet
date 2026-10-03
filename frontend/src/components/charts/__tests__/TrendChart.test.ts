// @vitest-environment jsdom
/**
 * Tests de componente de TrendChart.
 *
 * El lienzo de Chart.js no existe en jsdom —`getContext` devuelve null—, así que
 * se sustituye `vue-chartjs` por un doble que solo declara las props y captura
 * lo que el componente le pasa. Es justo lo que interesa comprobar: la
 * transformación de los registros en series y el reparto de ejes. El dibujado
 * real no es responsabilidad de este componente.
 */

import { describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'

vi.mock('vue-chartjs', () => ({
  Line: {
    name: 'Line',
    props: ['data', 'options'],
    template: '<canvas />',
  },
}))

import { Line } from 'vue-chartjs'

import TrendChart from '../TrendChart.vue'
import type { DailyLog, IsoDate } from '../../../types/daily-log'

function makeLog(logDate: IsoDate, overrides: Partial<DailyLog> = {}): DailyLog {
  return {
    id: `id-${logDate}`,
    logDate,
    moodRating: 3,
    anxietyLevel: 4,
    stressLevel: 5,
    sleepHours: 7,
    sleepQuality: 3,
    sleepDisturbances: [],
    activityType: null,
    activityMinutes: null,
    socialFrequency: 'occasional',
    symptoms: [],
    notes: null,
    createdAt: '2026-08-01T00:00:00.000Z',
    updatedAt: '2026-08-01T00:00:00.000Z',
    ...overrides,
  }
}

interface ChartData {
  labels: string[]
  datasets: { label: string; data: number[]; yAxisID: string }[]
}

describe('TrendChart', () => {
  describe('estado vacío', () => {
    it('muestra el amanecer y el acompañamiento en vez de un lienzo en blanco', () => {
      const wrapper = mount(TrendChart, { props: { logs: [], metricKeys: ['mood'] } })

      expect(wrapper.text()).toContain('No data yet')
      expect(wrapper.text()).toContain('Your first entry starts the story.')
      expect(wrapper.find('svg').exists()).toBe(true)
      expect(wrapper.findComponent(Line).exists()).toBe(false)
    })
  })

  describe('transformación de datos', () => {
    const logs = [
      makeLog('2026-10-01', { moodRating: 2, sleepHours: 6 }),
      makeLog('2026-10-02', { moodRating: 5, sleepHours: 8 }),
    ]

    it('usa las fechas como etiquetas y una serie por métrica', () => {
      const wrapper = mount(TrendChart, {
        props: { logs, metricKeys: ['mood', 'sleepHours'] },
      })
      const data = wrapper.findComponent(Line).props('data') as ChartData

      expect(data.labels).toEqual(['2026-10-01', '2026-10-02'])
      expect(data.datasets.map((dataset) => dataset.label)).toEqual(['Mood', 'Hours of sleep'])
    })

    it('extrae de cada registro el valor de cada métrica', () => {
      const wrapper = mount(TrendChart, {
        props: { logs, metricKeys: ['mood', 'sleepHours'] },
      })
      const data = wrapper.findComponent(Line).props('data') as ChartData

      expect(data.datasets[0].data).toEqual([2, 5])
      expect(data.datasets[1].data).toEqual([6, 8])
    })

    it('reparte las series entre el eje de escala y el de horas', () => {
      const wrapper = mount(TrendChart, {
        props: { logs, metricKeys: ['mood', 'sleepHours'] },
      })
      const chart = wrapper.findComponent(Line)
      const data = chart.props('data') as ChartData
      const options = chart.props('options') as {
        scales: Record<string, { position?: string }>
      }

      expect(data.datasets[0].yAxisID).toBe('scale')
      expect(data.datasets[1].yAxisID).toBe('hours')
      expect(options.scales.scale.position).toBe('left')
      expect(options.scales.hours.position).toBe('right')
    })

    it('no declara el eje de horas si no hay ninguna métrica de horas', () => {
      const wrapper = mount(TrendChart, { props: { logs, metricKeys: ['mood'] } })
      const options = wrapper.findComponent(Line).props('options') as {
        scales: Record<string, unknown>
      }

      expect(options.scales.hours).toBeUndefined()
    })
  })
})
