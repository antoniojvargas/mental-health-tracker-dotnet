<script setup lang="ts">
import {
  CategoryScale,
  Chart as ChartJS,
  LinearScale,
  LineElement,
  PointElement,
  Tooltip,
} from 'chart.js'
import type { ChartData, ChartOptions } from 'chart.js'
import { computed } from 'vue'
import { Line } from 'vue-chartjs'

import type { DailyLog } from '../../types/daily-log'
import { getMetric } from './metrics'

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, Tooltip)

/**
 * Gráfico de líneas del panel de tendencias.
 *
 * Cada métrica seleccionada es una serie, coloreada desde su propia definición
 * en `METRICS`. Las unidades no coinciden —ánimo va de 1 a 5, ansiedad hasta 10,
 * el sueño en horas y la actividad en minutos—, así que la escala no puede ser
 * única: las métricas de escala comparten el eje izquierdo y las de horas y
 * minutos el derecho. Los ejes se declaran solo si hay alguna serie que los use,
 * para no dejar un eje vacío al otro lado.
 *
 * La rejilla punteada y la ausencia de línea de eje salen de la misma opción:
 * Chart.js toma el trazo de la rejilla de `border.dash`, pero solo dibuja la
 * línea del eje cuando `border.display` es verdadero. Con `display: false` el
 * eje desaparece y la rejilla conserva el punteado.
 */

const GRID_COLOR = '#EAE0CB'
const TICK_COLOR = '#8A9B97'
const TICK_FONT = { family: '"IBM Plex Mono", monospace', size: 12 }
const TOOLTIP_BG = '#2B3A3A'
const TOOLTIP_TEXT = '#FBF8F2'

const LONG_DATE = new Intl.DateTimeFormat('en-US', { month: 'long', day: 'numeric' })

/**
 * "yyyy-MM-dd" a fecha larga en inglés ("October 3"), sin el desfase de huso de
 * `new Date(iso)`: el string ya viene como fecha local del servidor y parsearlo
 * como UTC restaría un día en husos positivos.
 */
function formatLongDate(iso: string): string {
  const [year, month, day] = iso.split('-').map(Number)
  return LONG_DATE.format(new Date(year, month - 1, day))
}

function axis(position: 'left' | 'right') {
  return {
    type: 'linear' as const,
    position,
    border: { display: false, dash: [3, 3] },
    grid: { color: GRID_COLOR, drawTicks: false },
    ticks: { color: TICK_COLOR, font: TICK_FONT },
  }
}

const props = defineProps<{
  /** Registros ya ordenados por fecha, que son los puntos de cada serie. */
  logs: DailyLog[]
  /** Claves de las métricas a pintar, en el orden de `METRICS`. */
  metricKeys: string[]
}>()

const metrics = computed(() => props.metricKeys.map(getMetric))

const chartData = computed<ChartData<'line'>>(() => ({
  labels: props.logs.map((log) => log.logDate),
  datasets: metrics.value.map((metric) => ({
    label: metric.label,
    data: props.logs.map((log) => metric.format(log)),
    borderColor: metric.color,
    backgroundColor: metric.color,
    yAxisID: metric.axis === 'hours' ? 'hours' : 'scale',
    borderWidth: 2.5,
    cubicInterpolationMode: 'monotone',
    pointRadius: 3,
    pointHoverRadius: 5,
    pointBackgroundColor: metric.color,
    pointBorderColor: metric.color,
    spanGaps: true,
  })),
}))

const chartOptions = computed<ChartOptions<'line'>>(() => {
  const scales: NonNullable<ChartOptions<'line'>['scales']> = {
    x: {
      type: 'category',
      border: { display: false, dash: [3, 3] },
      grid: { color: GRID_COLOR, drawTicks: false },
      ticks: { color: TICK_COLOR, font: TICK_FONT },
    },
  }

  if (metrics.value.some((metric) => metric.axis === 'scale')) {
    scales.scale = axis('left')
  }
  if (metrics.value.some((metric) => metric.axis === 'hours')) {
    scales.hours = axis('right')
  }

  return {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { mode: 'index', intersect: false },
    animation: { duration: 300 },
    plugins: {
      tooltip: {
        backgroundColor: TOOLTIP_BG,
        titleColor: TOOLTIP_TEXT,
        bodyColor: TOOLTIP_TEXT,
        cornerRadius: 8,
        padding: 12,
        usePointStyle: true,
        boxPadding: 4,
        titleFont: { family: '"IBM Plex Mono", monospace', size: 12, weight: 500 },
        bodyFont: { family: '"Newsreader", Georgia, ui-serif, serif', size: 12 },
        callbacks: {
          title: (items) => formatLongDate(String(items[0]?.label ?? '')),
          label: (item) => {
            const metric = metrics.value[item.datasetIndex]
            const value = item.parsed.y ?? 0
            return metric ? `${metric.label}: ${metric.describe(value)}` : ''
          },
        },
      },
    },
    scales,
  }
})
</script>

<template>
  <div class="h-72">
    <Line :data="chartData" :options="chartOptions" />
  </div>
</template>
