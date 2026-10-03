import type { DailyLog } from '../../types/daily-log'

export interface MetricDef {
  key: string
  label: string
  color: string
  axis: 'scale' | 'hours'
  format: (log: DailyLog) => number
  describe: (value: number) => string
}

function scaleLabel(value: number, labels: string[]): string {
  const idx = Math.max(0, Math.min(labels.length - 1, Math.round(value) - 1))
  return labels[idx]
}

export const METRICS: MetricDef[] = [
  {
    key: 'mood',
    label: 'Mood',
    color: '#7C9473',
    axis: 'scale',
    format: (l) => l.moodRating,
    describe: (v) => scaleLabel(v, ['very low', 'low', 'neutral', 'good', 'very good']),
  },
  {
    key: 'anxiety',
    label: 'Anxiety',
    color: '#6F94A6',
    axis: 'scale',
    format: (l) => l.anxietyLevel,
    describe: (v) => `${v}/10`,
  },
  {
    key: 'stress',
    label: 'Stress',
    color: '#C97C5D',
    axis: 'scale',
    format: (l) => l.stressLevel,
    describe: (v) => `${v}/10`,
  },
  {
    key: 'sleepHours',
    label: 'Hours of sleep',
    color: '#465F6C',
    axis: 'hours',
    format: (l) => l.sleepHours,
    describe: (v) => `${v} h`,
  },
  {
    key: 'sleepQuality',
    label: 'Sleep quality',
    color: '#A3BE95',
    axis: 'scale',
    format: (l) => l.sleepQuality,
    describe: (v) => scaleLabel(v, ['very poor', 'poor', 'fair', 'good', 'very good']),
  },
  {
    key: 'activityMinutes',
    label: 'Activity minutes',
    color: '#63775B',
    axis: 'hours',
    format: (l) => l.activityMinutes ?? 0,
    describe: (v) => `${v} min`,
  },
  {
    key: 'symptomLoad',
    label: 'Symptom load',
    color: '#33453F',
    axis: 'scale',
    format: (l) =>
      l.symptoms.length
        ? l.symptoms.reduce((sum, s) => sum + s.severity, 0) / l.symptoms.length
        : 0,
    describe: (v) => (v === 0 ? 'no symptoms' : `${v.toFixed(1)}/5`),
  },
]

export function getMetric(key: string): MetricDef {
  return METRICS.find((m) => m.key === key) ?? METRICS[0]
}
