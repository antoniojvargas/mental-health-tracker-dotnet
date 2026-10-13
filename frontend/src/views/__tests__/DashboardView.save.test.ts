// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { mount, type VueWrapper } from '@vue/test-utils'
import { nextTick } from 'vue'

import DashboardView from '../DashboardView.vue'
import { useAuthStore } from '../../stores/useAuthStore'
import type { DailyLog, IsoDate } from '../../types/daily-log'
import { todayIso } from '../../utils/date'

vi.mock('vue-router', () => ({
  useRouter: () => ({ replace: vi.fn() }),
}))

vi.mock('../../composables/useLogSocket', () => ({
  useLogSocket: vi.fn(),
}))

function makeLog(logDate: IsoDate): DailyLog {
  return {
    id: `id-${logDate}`,
    logDate,
    moodRating: 5,
    anxietyLevel: 5,
    stressLevel: 5,
    sleepHours: 7,
    sleepQuality: 4,
    sleepDisturbances: [],
    activityType: 'none',
    activityMinutes: null,
    socialFrequency: 'none',
    symptoms: [],
    notes: null,
    createdAt: '2026-09-13T17:18:21.1234567+00:00',
    updatedAt: '2026-09-13T17:18:21.1234567+00:00',
  }
}

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

let wrapper: VueWrapper | null = null

async function settle() {
  await nextTick()
  await new Promise((r) => setTimeout(r, 0))
  await nextTick()
}

function click(text: string) {
  const button = Array.from(document.querySelectorAll('button')).find(
    (b) => b.textContent?.trim() === text,
  )
  expect(button, `botón "${text}"`).toBeTruthy()
  button!.click()
}

describe('DashboardView · guardar el registro de hoy (A9)', () => {
  const saved = makeLog(todayIso())
  let requests: { method: string; path: string }[]

  beforeEach(() => {
    document.body.innerHTML = ''
    requests = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: string, init?: RequestInit) => {
        const method = init?.method ?? 'GET'
        const path = String(input)
        requests.push({ method, path })
        if (method === 'POST') return json(saved, 201)
        if (path.startsWith('/api/logs/today'))
          return json({ message: 'no', code: 'NOT_FOUND' }, 404)
        return json({
          data: [],
          meta: { from: todayIso(), to: todayIso(), limit: 100, offset: 0, total: 0 },
        })
      }),
    )
  })

  afterEach(() => {
    wrapper?.unmount()
    wrapper = null
    vi.unstubAllGlobals()
  })

  it('guardar hace una sola petición (el POST) y el saludo y las gráficas reflejan el registro', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    useAuthStore().user = { id: 'u', email: 'ana@example.com', name: 'Ana Gómez', avatarUrl: null }

    wrapper = mount(DashboardView, {
      attachTo: document.body,
      global: {
        plugins: [pinia],
        stubs: { RangeToggle: true, MetricSelector: true, TrendChart: true },
      },
    })
    await settle()
    expect(wrapper.text()).toContain('How are you feeling today? Take a minute to log it.')

    click('Log my day')
    await settle()
    // Ya se pidió el dashboard y el registro de hoy; solo importa lo que pasa al guardar.
    requests.length = 0

    click('Save now')
    await settle()

    expect(requests).toEqual([{ method: 'POST', path: '/api/logs' }])
    expect(wrapper.text()).toContain(
      "You already logged how you're feeling today. You can adjust it if something changed.",
    )
    const trend = wrapper.findComponent({ name: 'TrendChart' })
    expect((trend.props('logs') as DailyLog[]).map((l) => l.logDate)).toEqual([saved.logDate])
  })
})
