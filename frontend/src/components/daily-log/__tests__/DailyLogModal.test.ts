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
import {
  ACTIVITY_TYPES,
  SLEEP_DISTURBANCES,
  SOCIAL_FREQUENCIES,
  SYMPTOM_TYPES,
  type ActivityType,
  type DailyLog,
  type SleepDisturbance,
  type SymptomType,
} from '../../../types/daily-log'

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
// Ida y vuelta: abrir el modal con un registro y guardar sin tocar nada debe enviar a
// `create` exactamente los mismos campos que el registro (A5 del Apéndice de MEJORAS.md).
//
// Es la especificación del formulario sin pérdida. Los casos que hoy fallan están
// marcados con `it.fails` y el comentario "A5": el CI sigue en verde y, cuando un cambio
// corrige el mapeo, el caso empieza a pasar, `it.fails` lo delata y hay que convertirlo
// en `it`. No se debe borrar un caso para "arreglar" la suite.
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

  /** `it.fails` para lo que hoy pierde datos (A5); `it` para lo que ya viaja bien. */
  const spec = (knownLoss: boolean) => (knownLoss ? it.fails : it)

  beforeEach(() => {
    vi.mocked(logsApi.create).mockReset()
    vi.mocked(logsApi.today).mockReset()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    wrapper?.unmount()
    document.body.innerHTML = ''
  })

  it('un registro base sin enums especiales vuelve igual', async () => {
    await saveUntouchedAndCompare(makeLog())
  })

  describe('SocialFrequency', () => {
    for (const value of SOCIAL_FREQUENCIES) {
      it(`${value}: el payload conserva el valor`, async () => {
        await saveUntouchedAndCompare({ ...makeLog(), socialFrequency: value })
      })

      // El select solo ofrece none/once/few/several/daily: con rare, occasional y frequent
      // el control queda sin opción seleccionada y el usuario ve el campo en blanco. A5.
      spec(value === 'rare' || value === 'occasional' || value === 'frequent')(
        `${value}: el select muestra una opción seleccionada`,
        async () => {
          await openWith({ ...makeLog(), socialFrequency: value })
          clickNext()
          await settle()
          clickNext()
          await settle()

          const select = document.getElementById('socialFrequency') as HTMLSelectElement
          expect(select).toBeTruthy()
          expect(select.selectedIndex).not.toBe(-1)
        },
      )
    }
  })

  describe('ActivityType', () => {
    for (const value of ACTIVITY_TYPES) {
      // `none` se envía hoy como `null`: el valor del enum no vuelve igual. A5.
      spec(value === 'none')(`${value}: el payload conserva tipo y minutos`, async () => {
        await saveUntouchedAndCompare({
          ...makeLog(),
          activityType: value as ActivityType,
          activityMinutes: value === 'none' ? null : 30,
        })
      })
    }

    it('sin actividad (null): vuelve como null y sin minutos', async () => {
      await saveUntouchedAndCompare({ ...makeLog(), activityType: null, activityMinutes: null })
    })
  })

  describe('SleepDisturbance', () => {
    // `none` no es una alteración: la ausencia se representa con la lista vacía (caso base).
    const disturbances = SLEEP_DISTURBANCES.filter(
      (value): value is Exclude<SleepDisturbance, 'none'> => value !== 'none',
    )

    for (const value of disturbances) {
      // `early_waking` vuelve como `frequent_waking`. A5.
      spec(value === 'early_waking')(`${value}: el payload conserva la alteración`, async () => {
        await saveUntouchedAndCompare({ ...makeLog(), sleepDisturbances: [value] })
      })
    }

    // Varias a la vez. Falla por `early_waking`. A5.
    spec(true)('todas juntas: el payload conserva la lista completa', async () => {
      await saveUntouchedAndCompare({ ...makeLog(), sleepDisturbances: disturbances })
    })
  })

  describe('SymptomType', () => {
    SYMPTOM_TYPES.forEach((type: SymptomType) => {
      // `hopelessness` se pinta como "Depression" y vuelve como `low_mood`. A5.
      spec(type === 'hopelessness')(`${type}: conserva tipo y severidad`, async () => {
        await saveUntouchedAndCompare({
          ...makeLog(),
          symptoms: [{ type, severity: 4 }],
        })
      })
    })

    // Los 8 a la vez, cada uno con su severidad. Falla porque `low_mood` y `hopelessness`
    // comparten casilla ("Depression"): se pierde uno de los dos. A5.
    spec(true)('todos juntos: conserva cada tipo con su severidad', async () => {
      await saveUntouchedAndCompare({
        ...makeLog(),
        symptoms: SYMPTOM_TYPES.map((type, index) => ({ type, severity: (index % 5) + 1 })),
      })
    })
  })
})
