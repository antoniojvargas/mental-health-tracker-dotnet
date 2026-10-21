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
import { SOCIAL_FREQUENCIES, type DailyLog } from '../../../types/daily-log'
import { makeLog } from '../../../test-support/factories'

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

  describe('el día del cliente (20:00 en America/Bogota, ya es el día siguiente en UTC)', () => {
    beforeEach(() => {
      vi.stubEnv('TZ', 'America/Bogota')
      // Solo `Date`: los timers reales siguen haciendo falta para esperar a Vue.
      vi.useFakeTimers({ toFake: ['Date'] })
      vi.setSystemTime(new Date('2026-03-16T01:00:00Z'))
    })

    afterEach(() => {
      vi.useRealTimers()
      vi.unstubAllEnvs()
    })

    async function openModal() {
      wrapper = mount(Harness, { attachTo: document.body })
      document.getElementById('open')?.click()
      await nextTick()
      await nextTick()
      await new Promise((r) => setTimeout(r, 0))
    }

    it('pide el registro de hoy con la fecha local y precarga el formulario', async () => {
      // A estas horas el día UTC ya es el 16: la petición debe llevar el 15 local.
      expect(new Date().toISOString().slice(0, 10)).toBe('2026-03-16')
      vi.mocked(logsApi.today).mockResolvedValue({
        ...makeLog(),
        logDate: '2026-03-15',
        moodRating: 4,
      })

      await openModal()

      expect(logsApi.today).toHaveBeenCalledTimes(1)
      expect(logsApi.today).toHaveBeenCalledWith('2026-03-15')
      const checked = document.querySelector('[role="radio"][aria-checked="true"]')
      expect(checked?.textContent?.trim()).toBe('4')
    })

    it('guarda con la misma fecha local con la que consultó', async () => {
      vi.mocked(logsApi.today).mockResolvedValue(null)
      vi.mocked(logsApi.create).mockResolvedValue(makeLog())

      await openModal()
      selectMood(3)
      clickSaveNow()
      await nextTick()
      await nextTick()
      await new Promise((r) => setTimeout(r, 0))

      expect(logsApi.today).toHaveBeenCalledWith('2026-03-15')
      expect(logsApi.create).toHaveBeenCalledWith(
        expect.objectContaining({ logDate: '2026-03-15' }),
      )
    })
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

// ---------------------------------------------------------------------------
// Ida y vuelta a través del componente: abrir el modal con un registro y guardar sin tocar nada
// debe enviar a `create` exactamente los mismos campos que el registro (A5 del Apéndice de
// MEJORAS.md). La especificación completa, caso por caso, está en daily-log-mapper.test.ts.
// ---------------------------------------------------------------------------
describe('DailyLogModal · ida y vuelta sin pérdida (A5)', () => {
  const ROUND_TRIP_KEYS = [
    'moodRating',
    'anxietyLevel',
    'stressLevel',
    'sleepHours',
    'sleepQuality',
    'sleepDisturbances',
    'activityType',
    'activityMinutes',
    'socialFrequency',
    'symptoms',
    'notes',
  ] as const

  /** Los campos que viajan en el POST, con los síntomas en orden estable (el orden no es contrato). */
  function roundTripFields(source: Record<string, unknown>) {
    const picked: Record<string, unknown> = {}
    for (const key of ROUND_TRIP_KEYS) {
      picked[key] = source[key]
    }
    const symptoms = (picked.symptoms as { type: string; severity: number }[]).map((s) => ({
      ...s,
    }))
    picked.symptoms = symptoms.sort((a, b) => a.type.localeCompare(b.type))
    return picked
  }

  async function settle() {
    await nextTick()
    await nextTick()
    await new Promise((r) => setTimeout(r, 0))
  }

  async function openWith(log: DailyLog) {
    vi.mocked(logsApi.today).mockResolvedValue(log)
    vi.mocked(logsApi.create).mockResolvedValue(makeLog())
    wrapper = mount(Harness, { attachTo: document.body })
    document.getElementById('open')?.click()
    await settle()
  }

  async function saveUntouchedAndCompare(log: DailyLog) {
    await openWith(log)
    clickSaveNow()
    await settle()

    expect(logsApi.create).toHaveBeenCalledTimes(1)
    const sent = vi.mocked(logsApi.create).mock.calls[0][0] as unknown as Record<string, unknown>
    expect(roundTripFields(sent)).toEqual(roundTripFields({ ...log }))
  }

  beforeEach(() => {
    vi.mocked(logsApi.create).mockReset()
    vi.mocked(logsApi.today).mockReset()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    wrapper?.unmount()
    document.body.innerHTML = ''
  })

  // Los casos por valor de cada enum (payload que se reenvía) viven en daily-log-mapper.test.ts, donde
  // se prueban sin montar el componente. Aquí queda lo que solo el modal puede comprobar: que el
  // formulario que pinta se corresponde con el registro y que guardar envía el mapeo.
  it('un registro base sin enums especiales vuelve igual', async () => {
    await saveUntouchedAndCompare(makeLog())
  })

  it('un registro con todos los campos rellenos vuelve igual al guardarlo sin tocar nada', async () => {
    await saveUntouchedAndCompare({
      ...makeLog(),
      moodRating: 4,
      anxietyLevel: 7,
      stressLevel: 2,
      sleepHours: 6.5,
      sleepQuality: 2,
      sleepDisturbances: ['insomnia', 'early_waking'],
      activityType: 'cycling',
      activityMinutes: 45,
      socialFrequency: 'daily',
      symptoms: [
        { type: 'panic', severity: 5 },
        { type: 'low_mood', severity: 2 },
      ],
      notes: 'una nota',
    })
  })

  describe('SocialFrequency', () => {
    for (const value of SOCIAL_FREQUENCIES) {
      it(`${value}: el select muestra una opción seleccionada`, async () => {
        await openWith({ ...makeLog(), socialFrequency: value })
        clickNext()
        await settle()
        clickNext()
        await settle()

        const select = document.getElementById('socialFrequency') as HTMLSelectElement
        expect(select).toBeTruthy()
        expect(select.selectedIndex).not.toBe(-1)
        expect(select.value).toBe(value)
      })
    }
  })
})
