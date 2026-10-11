// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { nextTick, defineComponent, h, ref } from 'vue'
import DailyLogModal from '../DailyLogModal.vue'
import Toast from '../../ui/Toast.vue'

const toastSuccess = vi.fn()
const toastError = vi.fn()

vi.mock('../../../services/logs.api', () => ({
  create: vi.fn(),
  today: vi.fn(),
}))

vi.mock('../../../composables/useToast', () => ({
  useToast: () => ({
    success: toastSuccess,
    error: toastError,
    toasts: { value: [] },
    dismiss: vi.fn(),
    clear: vi.fn(),
  }),
}))

import { ApiError } from '../../../services/api-client'
import * as logsApi from '../../../services/logs.api'
import type { DailyLog } from '../../../types/daily-log'

function makeLog(): DailyLog {
  return {
    id: '1',
    logDate: '2026-01-01',
    moodRating: 3,
    anxietyLevel: 5,
    stressLevel: 5,
    sleepHours: 7,
    sleepQuality: 3,
    sleepDisturbances: [],
    activityType: null,
    activityMinutes: null,
    socialFrequency: 'none',
    symptoms: [],
    notes: null,
    createdAt: '2026-01-01T00:00:00.0000000+00:00',
    updatedAt: '2026-01-01T00:00:00.0000000+00:00',
  }
}

const Harness = defineComponent({
  components: { DailyLogModal, Toast },
  setup() {
    const open = ref(false)
    const saved = vi.fn()
    const submit = vi.fn()
    const close = vi.fn()
    return { open, saved, submit, close }
  },
  render() {
    return h('div', [
      h('button', { id: 'open', onClick: () => (this.open = true) }, 'Open'),
      h(DailyLogModal, {
        open: this.open,
        onClose: () => {
          this.open = false
          this.close()
        },
        onSubmit: this.submit,
        onSaved: this.saved,
      }),
      h(Toast),
    ])
  },
})

let wrapper: VueWrapper | null = null

function clickNext() {
  const nextBtn = Array.from(document.querySelectorAll('button')).find(
    (b) => b.textContent?.trim() === 'Next',
  )
  nextBtn?.click()
}

function clickSaveNow() {
  const saveNow = Array.from(document.querySelectorAll('button')).find(
    (b) => b.textContent?.trim() === 'Save now',
  )
  saveNow?.click()
}

function selectMood(value: number) {
  const moods = Array.from(document.querySelectorAll<HTMLElement>('[role="radio"]'))
  const mood = moods.find((r) => r.textContent?.trim() === String(value))
  mood?.click()
}

describe('DailyLogModal', () => {
  beforeEach(() => {
    vi.mocked(logsApi.create).mockReset()
    vi.mocked(logsApi.today).mockReset()
    toastSuccess.mockReset()
    toastError.mockReset()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    wrapper?.unmount()
    document.body.innerHTML = ''
  })

  it('navigates between steps', async () => {
    vi.mocked(logsApi.today).mockResolvedValue(null)
    wrapper = mount(Harness, { attachTo: document.body })
    document.getElementById('open')?.click()
    await nextTick()
    await nextTick()
    await new Promise((r) => setTimeout(r, 0))

    expect(document.body.textContent).toContain('Step 1 of 4')
    expect(document.body.textContent).toContain('Mood')

    selectMood(4)
    await nextTick()
    await nextTick()
    await new Promise((r) => setTimeout(r, 0))
    clickNext()
    await nextTick()
    await nextTick()
    await new Promise((r) => setTimeout(r, 0))
    expect(document.body.textContent).toContain('Step 2 of 4')
    expect(document.body.textContent).toContain('Sleep')

    clickNext()
    await nextTick()
    await new Promise((r) => setTimeout(r, 0))
    expect(document.body.textContent).toContain('Step 3 of 4')
    expect(document.body.textContent).toContain('Activity and social life')

    clickNext()
    await nextTick()
    await new Promise((r) => setTimeout(r, 0))
    expect(document.body.textContent).toContain('Step 4 of 4')
    expect(document.body.textContent).toContain('Symptoms')
  })

  it('can save from first step', async () => {
    vi.mocked(logsApi.today).mockResolvedValue(null)
    vi.mocked(logsApi.create).mockResolvedValue(makeLog())
    wrapper = mount(Harness, { attachTo: document.body })
    document.getElementById('open')?.click()
    await nextTick()
    await nextTick()
    await new Promise((r) => setTimeout(r, 0))

    selectMood(5)
    await nextTick()
    clickSaveNow()
    await nextTick()
    await nextTick()
    await new Promise((r) => setTimeout(r, 0))

    expect(logsApi.create).toHaveBeenCalled()
    expect(toastSuccess).toHaveBeenCalled()
  })

  it('sends expected payload on save', async () => {
    vi.mocked(logsApi.today).mockResolvedValue(null)
    vi.mocked(logsApi.create).mockResolvedValue(makeLog())
    wrapper = mount(Harness, { attachTo: document.body })
    document.getElementById('open')?.click()
    await nextTick()
    await nextTick()
    await new Promise((r) => setTimeout(r, 0))

    selectMood(3)
    clickSaveNow()
    await nextTick()
    await nextTick()
    await new Promise((r) => setTimeout(r, 0))

    expect(logsApi.create).toHaveBeenCalledWith(
      expect.objectContaining({
        moodRating: 3,
        anxietyLevel: expect.any(Number),
        stressLevel: expect.any(Number),
        sleepHours: expect.any(Number),
        sleepQuality: expect.any(Number),
        sleepDisturbances: expect.any(Array),
        symptoms: expect.any(Array),
        notes: null,
      }),
    )
  })

  it('shows toast on success and closes modal', async () => {
    vi.mocked(logsApi.today).mockResolvedValue(null)
    vi.mocked(logsApi.create).mockResolvedValue(makeLog())
    wrapper = mount(Harness, { attachTo: document.body })
    document.getElementById('open')?.click()
    await nextTick()
    await nextTick()
    await new Promise((r) => setTimeout(r, 0))

    selectMood(4)
    clickSaveNow()
    await nextTick()
    await nextTick()
    await new Promise((r) => setTimeout(r, 0))

    expect(toastSuccess).toHaveBeenCalledWith(expect.stringContaining('Thanks'))
    expect(document.body.textContent).not.toContain('Step 1')
  })

  it('shows error without closing modal when API fails', async () => {
    vi.mocked(logsApi.today).mockResolvedValue(null)
    vi.mocked(logsApi.create).mockRejectedValue(
      new ApiError(400, { code: 'VALIDATION_ERROR', message: 'Invalid', details: [] }),
    )
    wrapper = mount(Harness, { attachTo: document.body })
    document.getElementById('open')?.click()
    await nextTick()
    await nextTick()
    await new Promise((r) => setTimeout(r, 0))

    selectMood(2)
    clickSaveNow()
    await nextTick()
    await nextTick()
    await new Promise((r) => setTimeout(r, 0))

    // Se avisa con el texto traducido, no con el `message` crudo de la API.
    expect(toastError).toHaveBeenCalledWith(
      'Some entries need another look. Please review and try again.',
    )
    expect(document.body.textContent).toContain('Step 1 of 4')
  })

  it('anuncia la carga y bloquea avanzar sin quitar el formulario del foco', async () => {
    // La promesa no se resuelve: el modal se queda en su estado de carga.
    vi.mocked(logsApi.today).mockReturnValue(new Promise(() => {}))
    wrapper = mount(Harness, { attachTo: document.body })
    document.getElementById('open')?.click()
    await nextTick()
    await nextTick()
    await new Promise((r) => setTimeout(r, 0))

    expect(document.body.textContent).toContain("We're getting today's entry ready…")
    // El formulario sigue montado: el foco inicial debe poder caer en él.
    expect(document.body.textContent).toContain('Mood')
    // Pero no se puede avanzar ni guardar hasta que la carga termine.
    expect(
      Array.from(document.querySelectorAll('button')).some((b) => b.textContent?.trim() === 'Next'),
    ).toBe(false)
  })

  it('avisa y ofrece reintentar cuando no puede leer el registro de hoy', async () => {
    vi.mocked(logsApi.today).mockRejectedValue(
      new ApiError(500, { code: 'INTERNAL_ERROR', message: 'boom', details: [] }),
    )
    wrapper = mount(Harness, { attachTo: document.body })
    document.getElementById('open')?.click()
    await nextTick()
    await nextTick()
    await new Promise((r) => setTimeout(r, 0))

    expect(document.body.textContent).toContain(
      'Something went wrong on our end. Please try again.',
    )
    expect(document.body.textContent).not.toContain('boom')
    expect(document.body.textContent).not.toContain('Step 1 of 4')

    const tryAgain = Array.from(document.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === 'Try again',
    )
    expect(tryAgain).toBeTruthy()

    // El reintento sí resuelve: la carga se recupera y el formulario aparece.
    vi.mocked(logsApi.today).mockResolvedValue(null)
    tryAgain?.click()
    await nextTick()
    await nextTick()
    await new Promise((r) => setTimeout(r, 0))

    expect(document.body.textContent).toContain('Step 1 of 4')
  })
})
