// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { mount, type VueWrapper } from '@vue/test-utils'

import DashboardView from '../DashboardView.vue'
import Skeleton from '../../components/ui/Skeleton.vue'
import { useAuthStore } from '../../stores/useAuthStore'
import { useLogsStore } from '../../stores/useLogsStore'
import type { DailyLog, IsoDate, User } from '../../types/daily-log'

vi.mock('vue-router', () => ({
  useRouter: () => ({ replace: vi.fn() }),
}))

function makeLog(logDate: IsoDate): DailyLog {
  return {
    id: `id-${logDate}`,
    logDate,
    moodRating: 3,
    anxietyLevel: 5,
    stressLevel: 5,
    sleepHours: 7,
    sleepQuality: 4,
    sleepDisturbances: [],
    activityType: null,
    activityMinutes: null,
    socialFrequency: 'none',
    symptoms: [],
    notes: null,
    createdAt: '2026-09-13T17:18:21.1234567+00:00',
    updatedAt: '2026-09-13T17:18:21.1234567+00:00',
  }
}

function makeUser(name: string): User {
  return { id: 'user-1', email: 'ana@example.com', name, avatarUrl: null }
}

/** "Hoy" en hora local, igual que lo calcula la vista. */
function todayIso(): IsoDate {
  const now = new Date()
  const month = String(now.getMonth() + 1).padStart(2, '0')
  const day = String(now.getDate()).padStart(2, '0')
  return `${now.getFullYear()}-${month}-${day}`
}

interface Options {
  user?: User | null
  logs?: DailyLog[]
  loading?: boolean
  error?: string | null
}

let wrapper: VueWrapper | null = null

function mountDashboard(options: Options = {}): VueWrapper {
  const pinia = createPinia()
  setActivePinia(pinia)

  useAuthStore().user = options.user ?? null

  const logs = useLogsStore()
  vi.spyOn(logs, 'fetch').mockResolvedValue(undefined)
  logs.logs = options.logs ?? []
  logs.loading = options.loading ?? false
  logs.error = options.error ?? null

  wrapper = mount(DashboardView, {
    global: {
      plugins: [pinia],
      stubs: {
        RangeToggle: true,
        MetricSelector: true,
        TrendChart: true,
        DailyLogModal: true,
      },
    },
  })
  return wrapper
}

function buttonByText(text: string) {
  return wrapper!.findAll('button').find((button) => button.text() === text)
}

describe('DashboardView', () => {
  beforeEach(() => {
    document.body.innerHTML = ''
  })

  afterEach(() => {
    wrapper?.unmount()
    wrapper = null
    vi.restoreAllMocks()
  })

  it('saluda por el primer nombre', () => {
    mountDashboard({ user: makeUser('Ana Gómez') })

    expect(wrapper!.text()).toContain('Hi, Ana.')
  })

  it('sin sesión saluda sin arriesgar un nombre inventado', () => {
    mountDashboard({ user: null })

    expect(wrapper!.text()).toContain('Hi.')
    expect(wrapper!.text()).not.toContain('Hi, ')
  })

  it('sin registro de hoy pide crear uno', () => {
    mountDashboard({ user: makeUser('Ana Gómez'), logs: [makeLog('2026-09-30')] })

    expect(wrapper!.text()).toContain('How are you feeling today? Take a minute to log it.')
    expect(buttonByText('Log my day')).toBeTruthy()
    expect(buttonByText("Adjust today's entry")).toBeUndefined()
  })

  it('con registro de hoy ofrece ajustarlo', () => {
    mountDashboard({ user: makeUser('Ana Gómez'), logs: [makeLog(todayIso())] })

    expect(wrapper!.text()).toContain(
      "You already logged how you're feeling today. You can adjust it if something changed.",
    )
    expect(buttonByText("Adjust today's entry")).toBeTruthy()
    expect(buttonByText('Log my day')).toBeUndefined()
  })

  it('muestra el esqueleto y el estado de espera mientras carga', () => {
    mountDashboard({ user: makeUser('Ana Gómez'), loading: true })

    expect(wrapper!.findComponent(Skeleton).exists()).toBe(true)
    expect(wrapper!.text()).toContain('Checking your day…')
  })

  it('retira el esqueleto cuando la carga terminó', () => {
    mountDashboard({ user: makeUser('Ana Gómez'), logs: [] })

    expect(wrapper!.findComponent(Skeleton).exists()).toBe(false)
  })
})
