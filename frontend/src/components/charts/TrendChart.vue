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
// ink-400 y no ink-300: el gris claro anterior se quedaba en 2.7:1 sobre el papel
// y las etiquetas de los ejes no llegaban al 3:1 que pide el texto de gráficos.
const TICK_COLOR = '#5D726D'
const TICK_FONT = { family: '"IBM Plex Mono", monospace', size: 12 }
const TOOLTIP_BG = '#2B3A3A'
const TOOLTIP_TEXT = '#FBF8F2'

/**
 * Etiquetas del eje X: el ISO crudo ("2026-10-05") ocupa casi todo el ancho de
 * la gráfica en un móvil y las marcas se pisan. La forma corta ("Oct 5") deja
 * seis días legibles. Se construye el `Date` por partes para no interpretar la
 * fecha como UTC y correrla un día.
 */
const SHORT_DATE = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric' })

function formatShortDate(iso: string): string {
  const [year, month, day] = iso.split('-').map(Number)
  return SHORT_DATE.format(new Date(year, month - 1, day))
}

/**
 * ¿Pide el usuario reducir el movimiento? Chart.js no lee `prefers-reduced-motion`
 * por su cuenta, así que hay que decírselo. Se lee una vez al montar con guarda:
 * `jsdom` (tests) no implementa `matchMedia`, y en un entorno sin ventana no hay
 * nada que preguntar — en ambos casos el valor honesto es "no reduce".
 */
const prefersReducedMotion =
  typeof window !== 'undefined' &&
  typeof window.matchMedia === 'function' &&
  window.matchMedia('(prefers-reduced-motion: reduce)').matches

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

/**
 * Nombre accesible del gráfico. Un `<canvas>` no tiene texto alternativo propio:
 * sin esto, un lector de pantalla no anuncia nada o anuncia "canvas". Se nombra
 * con las series visibles y el número de días para que la imagen tenga un
 * resumen útil sin volcar toda la tabla de datos.
 */
const chartLabel = computed(() => {
  const names = metrics.value.map((metric) => metric.label).join(', ')
  return names ? `Trend chart. Series: ${names}. ${props.logs.length} days shown.` : 'Trend chart'
})

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
      ticks: {
        color: TICK_COLOR,
        font: TICK_FONT,
        maxRotation: 0,
        maxTicksLimit: 6,
        callback: (_value, index) => formatShortDate(props.logs[index]?.logDate ?? ''),
      },
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
    // El trazo animado es la parte de la gráfica que más movimiento mete; con
    // `prefers-reduced-motion` se dibuja de golpe y no de 0 a 1.
    animation: { duration: prefersReducedMotion ? 0 : 300 },
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
  <div
    v-if="logs.length === 0"
    class="flex h-72 flex-col items-center justify-center rounded-2xl border border-ink-100 bg-paper-50 text-center"
  >
    <svg
      class="h-8 w-8 text-ink-300"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="1.5"
      stroke-linecap="round"
      aria-hidden="true"
    >
      <path d="M5 16a7 7 0 0 1 14 0" />
      <path d="M3 16h18M12 3v3M5.6 8.6l1.4 1.4M18.4 8.6 17 10" />
      <path d="M8 20h8" />
    </svg>
    <p class="mt-2 font-display font-medium text-ink-600">No data yet</p>
    <p class="text-sm text-ink-400">Your first entry starts the story.</p>
  </div>

  <div v-else class="h-72" role="img" :aria-label="chartLabel">
    <Line :data="chartData" :options="chartOptions" />
  </div>
</template>
