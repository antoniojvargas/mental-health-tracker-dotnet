// @vitest-environment jsdom
/**
 * Caracterización de la interacción de DailyLogModal, para proteger su división en
 * componentes por paso: la navegación entre pasos, el orden de foco y los ids de cada
 * control, los eventos que emite y los estados de carga y de error de la lectura de hoy.
 * El contenido del payload lo cubre el test de ida y vuelta de DailyLogModal.test.ts.
 *
 * No se prueba el cómo (qué componente pinta qué) sino lo que ve y usa quien interactúa:
 * textos, roles, ids y orden. Un refactor que lo respete deja estos tests en verde.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h, nextTick, ref } from 'vue'
import DailyLogModal from '../DailyLogModal.vue'
import type { DailyLogForm } from '../../../types/daily-log-form'
import { SYMPTOM_OPTIONS } from '../daily-log-options'

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
import { SYMPTOM_TYPES, type DailyLog } from '../../../types/daily-log'
import { todayIso } from '../../../utils/date'
import { makeLog } from '../../../test-support/factories'

// El orden en que el padre recibe los eventos importa: submit, saved y close.
const events: string[] = []
const submitted: DailyLogForm[] = []
const saved: DailyLog[] = []

const Harness = defineComponent({
  components: { DailyLogModal },
  setup() {
    return { open: ref(false) }
  },
  render() {
    return h('div', [
      h('button', { id: 'open', onClick: () => (this.open = true) }, 'Open'),
      h(DailyLogModal, {
        open: this.open,
        onClose: () => {
          events.push('close')
          this.open = false
        },
        onSubmit: (data: DailyLogForm) => {
          events.push('submit')
          submitted.push(data)
        },
        onSaved: (log: DailyLog) => {
          events.push('saved')
          saved.push(log)
        },
      }),
    ])
  },
})

let wrapper: VueWrapper | null = null

async function settle() {
  await nextTick()
  await nextTick()
  await new Promise((r) => setTimeout(r, 0))
}

async function openModal(log: DailyLog | null = null) {
  vi.mocked(logsApi.today).mockResolvedValue(log)
  wrapper = mount(Harness, { attachTo: document.body })
  document.getElementById('open')?.click()
  await settle()
}

const dialog = () => document.querySelector<HTMLElement>('[role="dialog"]')!

function button(text: string): HTMLButtonElement | undefined {
  return Array.from(dialog().querySelectorAll('button')).find((b) => b.textContent?.trim() === text)
}

async function press(text: string) {
  const target = button(text)
  expect(target, `botón "${text}"`).toBeTruthy()
  target!.click()
  await settle()
}

async function chooseMood(value: number) {
  const radio = Array.from(
    dialog().querySelectorAll<HTMLElement>('[role="radiogroup"][aria-label="Mood"] [role="radio"]'),
  ).find((r) => r.textContent?.trim() === String(value))
  radio!.click()
  await settle()
}

/** "Step 2 of 4: Sleep" o el texto de carga, tal como lo lee un lector de pantalla. */
const statusText = () =>
  Array.from(dialog().querySelectorAll('[role="status"]'))
    .map((s) => s.textContent?.replace(/\s+/g, ' ').trim())
    .join(' | ')

const progressWidth = () =>
  dialog().querySelector<HTMLElement>('div[aria-hidden="true"] > div')?.style.width

function visibleButtons(): string[] {
  return Array.from(dialog().querySelectorAll('button'))
    .filter((b) => !b.closest('form'))
    .map((b) => b.textContent?.trim() ?? '')
}

// Mismos elementos que considera el trap de foco del Modal, en orden de DOM.
const FOCUSABLE = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled]):not([type="hidden"])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(',')

/** Nombre estable de un control: su id fijo, o su etiqueta si el id es aleatorio (sliders). */
function describe_(el: HTMLElement): string {
  const label = (id: string) => document.querySelector(`label[for="${id}"]`)?.textContent?.trim()
  if (el instanceof HTMLInputElement && el.type === 'range') {
    return `slider "${label(el.id)}"`
  }
  if (el.id) return `#${el.id}`
  const group = el.closest('[role="radiogroup"]')?.getAttribute('aria-label')
  if (el.getAttribute('role') === 'radio') return `radio "${el.textContent?.trim()}" of "${group}"`
  if (el instanceof HTMLInputElement && el.type === 'checkbox') {
    return `checkbox "${el.value}" ${el.closest('fieldset')?.querySelector('legend')?.textContent?.trim()}`
  }
  return `${el.tagName.toLowerCase()} "${el.textContent?.trim()}"`
}

const focusOrder = () =>
  Array.from(dialog().querySelectorAll<HTMLElement>(FOCUSABLE)).map(describe_)

beforeEach(() => {
  events.length = 0
  submitted.length = 0
  saved.length = 0
  toastSuccess.mockReset()
  toastError.mockReset()
  vi.mocked(logsApi.create).mockReset()
  vi.mocked(logsApi.today).mockReset()
  document.body.innerHTML = ''
})

afterEach(() => {
  wrapper?.unmount()
  wrapper = null
  document.body.innerHTML = ''
})

describe('DailyLogModal · navegación entre pasos', () => {
  it('empieza en el paso 1 con Back deshabilitado y Next bloqueado hasta elegir el ánimo', async () => {
    await openModal()

    expect(statusText()).toBe('Step 1 of 4: Mood')
    expect(button('Back')!.disabled).toBe(true)
    expect(button('Next')!.disabled).toBe(true)

    await chooseMood(3)

    expect(button('Next')!.disabled).toBe(false)
  })

  it('pulsar Next sin ánimo no avanza', async () => {
    await openModal()

    await press('Next')

    expect(statusText()).toBe('Step 1 of 4: Mood')
  })

  it('recorre los cuatro pasos con su título, su sección y su barra de progreso', async () => {
    await openModal()
    await chooseMood(3)

    const seen: [string, string | undefined, string | null][] = []
    for (let step = 0; step < 4; step++) {
      seen.push([
        statusText(),
        progressWidth(),
        dialog().querySelector('form h3')?.textContent?.trim() ?? null,
      ])
      if (step < 3) await press('Next')
    }

    expect(seen).toEqual([
      ['Step 1 of 4: Mood', '25%', 'Mood'],
      ['Step 2 of 4: Sleep', '50%', 'Sleep'],
      ['Step 3 of 4: Activity and social life', '75%', 'Activity and social life'],
      ['Step 4 of 4: Symptoms', '100%', 'Symptoms'],
    ])
  })

  it('en los pasos 1-3 ofrece Next y Save now; en el último solo Save', async () => {
    await openModal()
    await chooseMood(3)

    for (let step = 0; step < 3; step++) {
      expect(visibleButtons()).toContain('Next')
      expect(visibleButtons()).toContain('Save now')
      expect(visibleButtons()).not.toContain('Save')
      await press('Next')
    }

    expect(visibleButtons()).toEqual(['Back', 'Cancel', 'Save'])
  })

  it('Back vuelve al paso anterior conservando lo escrito, y se deshabilita en el primero', async () => {
    await openModal()
    await chooseMood(5)
    await press('Next')
    await press('Next')
    expect(statusText()).toBe('Step 3 of 4: Activity and social life')

    await press('Back')
    expect(statusText()).toBe('Step 2 of 4: Sleep')
    expect(progressWidth()).toBe('50%')
    await press('Back')

    expect(statusText()).toBe('Step 1 of 4: Mood')
    expect(button('Back')!.disabled).toBe(true)
    const checked = dialog().querySelector(
      '[role="radiogroup"][aria-label="Mood"] [aria-checked="true"]',
    )
    expect(checked?.textContent?.trim()).toBe('5')
  })

  it('los valores de un paso sobreviven a ir y volver', async () => {
    await openModal()
    await chooseMood(2)
    await press('Next')
    const hours = dialog().querySelector<HTMLInputElement>(
      'input[type="range"][min="0"][max="24"]',
    )!
    hours.value = '9'
    hours.dispatchEvent(new Event('input'))
    await settle()
    await press('Next')
    await press('Back')

    const again = dialog().querySelector<HTMLInputElement>(
      'input[type="range"][min="0"][max="24"]',
    )!
    expect(again.value).toBe('9')
  })

  it('al cerrar y reabrir vuelve al paso 1', async () => {
    await openModal()
    await chooseMood(3)
    await press('Next')
    expect(statusText()).toBe('Step 2 of 4: Sleep')

    await press('Cancel')
    expect(events).toEqual(['close'])
    document.getElementById('open')?.click()
    await settle()

    expect(statusText()).toBe('Step 1 of 4: Mood')
  })
})

describe('DailyLogModal · orden de foco e ids', () => {
  it('el foco inicial cae en el primer control del formulario, no en un botón', async () => {
    await openModal()

    const active = document.activeElement as HTMLElement
    expect(active.getAttribute('role')).toBe('radio')
    expect(active.closest('[role="radiogroup"]')?.getAttribute('aria-label')).toBe('Mood')
  })

  it('paso 1 (Mood): ánimo, ansiedad, estrés y luego los botones', async () => {
    await openModal()

    expect(focusOrder()).toEqual([
      'radio "1" of "Mood"',
      'slider "Anxiety"',
      'slider "Stress"',
      'button "Cancel"',
      'button "Save now"',
    ])
  })

  it('paso 2 (Sleep): horas, calidad y las cuatro alteraciones, en ese orden', async () => {
    await openModal()
    await chooseMood(3)
    await press('Next')

    expect(focusOrder()).toEqual([
      'slider "Sleep hours"',
      'slider "Sleep quality"',
      'checkbox "insomnia" Sleep disturbances (select all that apply)',
      'checkbox "frequent_waking" Sleep disturbances (select all that apply)',
      'checkbox "early_waking" Sleep disturbances (select all that apply)',
      'checkbox "nightmares" Sleep disturbances (select all that apply)',
      'button "Back"',
      'button "Cancel"',
      'button "Next"',
      'button "Save now"',
    ])
  })

  it('paso 3: tipo de actividad, minutos (tras elegir actividad) y frecuencia social', async () => {
    await openModal()
    await chooseMood(3)
    await press('Next')
    await press('Next')

    // Sin actividad los minutos están deshabilitados y quedan fuera del orden de foco.
    expect(focusOrder()).toEqual([
      '#activityType',
      '#socialFrequency',
      'button "Back"',
      'button "Cancel"',
      'button "Next"',
      'button "Save now"',
    ])
    const minutes = dialog().querySelector<HTMLInputElement>('input[type="range"][max="600"]')!
    expect(minutes.disabled).toBe(true)

    const select = dialog().querySelector<HTMLSelectElement>('#activityType')!
    select.value = 'walking'
    select.dispatchEvent(new Event('change'))
    await settle()

    expect(minutes.disabled).toBe(false)
    expect(focusOrder()).toEqual([
      '#activityType',
      'slider "Activity minutes"',
      '#socialFrequency',
      'button "Back"',
      'button "Cancel"',
      'button "Next"',
      'button "Save now"',
    ])
  })

  it('paso 4: los ocho síntomas, la severidad del activo y las notas', async () => {
    await openModal()
    await chooseMood(3)
    await press('Next')
    await press('Next')
    await press('Next')

    expect(focusOrder()).toEqual([
      ...SYMPTOM_OPTIONS.map((o) => `#sym-check-${o.key}`),
      '#notes',
      'button "Back"',
      'button "Cancel"',
      'button "Save"',
    ])

    // Al activar un síntoma aparece su severidad justo después de su casilla.
    dialog().querySelector<HTMLInputElement>('#sym-check-fatigue')!.click()
    await settle()
    const order = focusOrder()
    const at = order.indexOf('#sym-check-fatigue')
    expect(order[at + 1]).toBe('radio "1" of "Fatigue severity"')
    expect(order.at(-4)).toBe('#notes')
  })

  it('los ids y etiquetas de cada control no cambian', async () => {
    await openModal()
    await chooseMood(3)

    const radiogroup = dialog().querySelector('[role="radiogroup"]')
    expect(radiogroup?.getAttribute('aria-label')).toBe('Mood')

    await press('Next')
    await press('Next')
    expect(dialog().querySelector('select#activityType')).toBeTruthy()
    expect(dialog().querySelector('label[for="activityType"]')?.textContent?.trim()).toBe(
      'Activity type',
    )
    expect(dialog().querySelector('select#socialFrequency')).toBeTruthy()
    expect(dialog().querySelector('label[for="socialFrequency"]')?.textContent?.trim()).toBe(
      'Social contact frequency',
    )

    await press('Next')
    expect(dialog().querySelector('textarea#notes')).toBeTruthy()
    expect(dialog().querySelector('label[for="notes"]')?.textContent?.trim()).toBe('Notes')
    expect(dialog().querySelector('fieldset[aria-label="Symptoms"]')).toBeTruthy()
    for (const type of SYMPTOM_TYPES) {
      expect(dialog().querySelector(`input#sym-check-${type}[type="checkbox"]`)).toBeTruthy()
    }
  })

  it('cada slider enlaza su etiqueta con su input por for/id', async () => {
    await openModal()
    await chooseMood(3)

    const names: string[] = []
    for (let step = 0; step < 3; step++) {
      dialog()
        .querySelectorAll<HTMLInputElement>('input[type="range"]')
        .forEach((input) => {
          const label = dialog().querySelector(`label[for="${input.id}"]`)
          expect(label, `label de ${input.id}`).toBeTruthy()
          names.push(label!.textContent!.trim())
        })
      await press('Next')
    }

    expect(names).toEqual(['Anxiety', 'Stress', 'Sleep hours', 'Sleep quality', 'Activity minutes'])
  })
})

describe('DailyLogModal · eventos', () => {
  it('guardar emite submit, saved con el registro de create y close, en ese orden', async () => {
    const created = makeLog({ id: 'from-api', moodRating: 5 })
    vi.mocked(logsApi.create).mockResolvedValue(created)
    await openModal()
    await chooseMood(5)

    await press('Save now')

    expect(events).toEqual(['submit', 'saved', 'close'])
    expect(saved).toHaveLength(1)
    expect(saved[0]).toBe(created)
    expect(toastSuccess).toHaveBeenCalledWith('Thanks for logging today!')
  })

  it('submit lleva una copia del formulario con lo que se rellenó', async () => {
    vi.mocked(logsApi.create).mockResolvedValue(makeLog())
    await openModal()
    await chooseMood(4)
    await press('Next')
    await press('Next')
    await press('Next')
    dialog().querySelector<HTMLInputElement>('#sym-check-fatigue')!.click()
    await settle()

    await press('Save')

    const form = submitted[0]
    expect(form.mood).toBe(4)
    expect(form.anxiety).toBe(5)
    expect(form.stress).toBe(5)
    expect(form.sleepQuality).toBe(3)
    expect(form.sleepHours).toBe(7)
    expect(form.sleepDisturbances).toEqual([])
    expect(form.activityType).toBe('none')
    expect(form.socialFrequency).toBe('none')
    expect(form.notes).toBe('')
    expect(form.symptoms.filter((s) => s.active).map((s) => s.key)).toEqual(['fatigue'])
  })

  it('tras guardar el formulario queda limpio: reabrir empieza de cero', async () => {
    vi.mocked(logsApi.create).mockResolvedValue(makeLog())
    await openModal()
    await chooseMood(5)
    await press('Save now')

    document.getElementById('open')?.click()
    await settle()

    expect(statusText()).toBe('Step 1 of 4: Mood')
    expect(
      dialog().querySelector('[role="radiogroup"][aria-label="Mood"] [aria-checked="true"]'),
    ).toBeNull()
  })

  it('guardar desde el último paso con Save emite lo mismo', async () => {
    vi.mocked(logsApi.create).mockResolvedValue(makeLog())
    await openModal()
    await chooseMood(3)
    await press('Next')
    await press('Next')
    await press('Next')

    await press('Save')

    expect(events).toEqual(['submit', 'saved', 'close'])
  })

  it('si create falla no emite nada, avisa, mantiene lo escrito y deja reintentar', async () => {
    vi.mocked(logsApi.create).mockRejectedValueOnce(
      new ApiError(500, { code: 'INTERNAL_ERROR', message: 'boom', details: [] }),
    )
    vi.mocked(logsApi.create).mockResolvedValueOnce(makeLog())
    await openModal()
    await chooseMood(5)

    await press('Save now')

    expect(events).toEqual([])
    expect(toastError).toHaveBeenCalledTimes(1)
    expect(toastError.mock.calls[0][0]).not.toContain('boom')
    expect(toastSuccess).not.toHaveBeenCalled()
    expect(dialog()).toBeTruthy()
    expect(statusText()).toBe('Step 1 of 4: Mood')
    expect(
      dialog()
        .querySelector('[role="radiogroup"][aria-label="Mood"] [aria-checked="true"]')
        ?.textContent?.trim(),
    ).toBe('5')

    await press('Save now')

    expect(logsApi.create).toHaveBeenCalledTimes(2)
    expect(events).toEqual(['submit', 'saved', 'close'])
  })

  it('un segundo clic mientras guarda no envía otra petición', async () => {
    let resolve!: (log: DailyLog) => void
    vi.mocked(logsApi.create).mockReturnValue(new Promise<DailyLog>((r) => (resolve = r)))
    await openModal()
    await chooseMood(3)

    button('Save now')!.click()
    button('Save now')!.click()
    await settle()
    expect(logsApi.create).toHaveBeenCalledTimes(1)

    resolve(makeLog())
    await settle()
    expect(events).toEqual(['submit', 'saved', 'close'])
  })

  it('Cancel emite close sin tocar la API', async () => {
    await openModal()

    await press('Cancel')

    expect(events).toEqual(['close'])
    expect(logsApi.create).not.toHaveBeenCalled()
  })

  it('Escape cierra el modal', async () => {
    await openModal()

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }))
    await settle()

    expect(events).toEqual(['close'])
  })
})

describe('DailyLogModal · lectura de hoy (loadToday)', () => {
  it('pide el registro del día local una vez por apertura', async () => {
    await openModal()

    expect(logsApi.today).toHaveBeenCalledTimes(1)
    expect(logsApi.today).toHaveBeenCalledWith(todayIso())

    await press('Cancel')
    document.getElementById('open')?.click()
    await settle()

    expect(logsApi.today).toHaveBeenCalledTimes(2)
  })

  it('mientras carga: anuncia la espera, mantiene el formulario y no ofrece avanzar ni guardar', async () => {
    vi.mocked(logsApi.today).mockReturnValue(new Promise(() => {}))
    wrapper = mount(Harness, { attachTo: document.body })
    document.getElementById('open')?.click()
    await settle()

    expect(statusText()).toBe("We're getting today's entry ready…")
    expect(dialog().querySelector('form h3')?.textContent?.trim()).toBe('Mood')
    expect(visibleButtons()).toEqual(['Back', 'Cancel'])
    expect(button('Back')!.disabled).toBe(true)
    // El foco inicial cae en el formulario, no en un botón.
    expect(document.activeElement?.getAttribute('role')).toBe('radio')
  })

  it('al terminar la carga aparecen el paso y los botones', async () => {
    let resolve!: (log: DailyLog | null) => void
    vi.mocked(logsApi.today).mockReturnValue(new Promise((r) => (resolve = r)))
    wrapper = mount(Harness, { attachTo: document.body })
    document.getElementById('open')?.click()
    await settle()

    resolve(null)
    await settle()

    expect(statusText()).toBe('Step 1 of 4: Mood')
    expect(visibleButtons()).toEqual(['Back', 'Cancel', 'Next', 'Save now'])
  })

  it('sin registro de hoy deja el formulario en sus valores por defecto', async () => {
    await openModal(null)

    expect(
      dialog().querySelector('[role="radiogroup"][aria-label="Mood"] [aria-checked="true"]'),
    ).toBeNull()
    const sliders = Array.from(dialog().querySelectorAll<HTMLInputElement>('input[type="range"]'))
    expect(sliders.map((s) => s.value)).toEqual(['5', '5'])
    expect(button('Next')!.disabled).toBe(true)
  })

  it('con registro de hoy rellena el formulario y permite avanzar desde el primer paso', async () => {
    await openModal(
      makeLog({
        logDate: todayIso(),
        moodRating: 4,
        anxietyLevel: 6,
        stressLevel: 3,
        sleepHours: 6.5,
        sleepQuality: 2,
        sleepDisturbances: ['nightmares'],
        activityType: 'running',
        activityMinutes: 45,
        socialFrequency: 'daily',
        symptoms: [{ type: 'panic', severity: 4 }],
        notes: 'ya registrado',
      }),
    )

    expect(
      dialog()
        .querySelector('[role="radiogroup"][aria-label="Mood"] [aria-checked="true"]')
        ?.textContent?.trim(),
    ).toBe('4')
    const sliders = Array.from(dialog().querySelectorAll<HTMLInputElement>('input[type="range"]'))
    expect(sliders.map((s) => s.value)).toEqual(['6', '3'])
    expect(button('Next')!.disabled).toBe(false)

    await press('Next')
    expect(
      Array.from(dialog().querySelectorAll<HTMLInputElement>('input[type="range"]')).map(
        (s) => s.value,
      ),
    ).toEqual(['6.5', '2'])
    expect(dialog().querySelector<HTMLInputElement>('input[value="nightmares"]')!.checked).toBe(
      true,
    )

    await press('Next')
    expect(dialog().querySelector<HTMLSelectElement>('#activityType')!.value).toBe('running')
    expect(dialog().querySelector<HTMLSelectElement>('#socialFrequency')!.value).toBe('daily')

    await press('Next')
    expect(dialog().querySelector<HTMLInputElement>('#sym-check-panic')!.checked).toBe(true)
    expect(dialog().querySelector<HTMLTextAreaElement>('#notes')!.value).toBe('ya registrado')
  })

  it('si falla la lectura muestra el aviso con reintento y sin formulario ni botones de avance', async () => {
    vi.mocked(logsApi.today).mockRejectedValue(
      new ApiError(500, { code: 'INTERNAL_ERROR', message: 'boom', details: [] }),
    )
    wrapper = mount(Harness, { attachTo: document.body })
    document.getElementById('open')?.click()
    await settle()

    expect(dialog().querySelector('[role="alert"]')?.textContent).toContain('Something went wrong')
    expect(dialog().querySelector('form')).toBeNull()
    expect(dialog().querySelector('[role="status"]')).toBeNull()
    expect(visibleButtons()).toEqual(['Try again', 'Back', 'Cancel'])
    expect(button('Back')!.disabled).toBe(true)
  })

  it('Try again repite la lectura: si sale bien aparece el formulario, si falla vuelve el aviso', async () => {
    vi.mocked(logsApi.today).mockRejectedValue(
      new ApiError(500, { code: 'INTERNAL_ERROR', message: 'boom', details: [] }),
    )
    wrapper = mount(Harness, { attachTo: document.body })
    document.getElementById('open')?.click()
    await settle()

    await press('Try again')
    expect(logsApi.today).toHaveBeenCalledTimes(2)
    expect(dialog().querySelector('[role="alert"]')).toBeTruthy()

    vi.mocked(logsApi.today).mockResolvedValue(null)
    await press('Try again')

    expect(logsApi.today).toHaveBeenCalledTimes(3)
    expect(dialog().querySelector('[role="alert"]')).toBeNull()
    expect(statusText()).toBe('Step 1 of 4: Mood')
  })

  it('reabrir tras un error vuelve a leer y descarta el aviso anterior', async () => {
    vi.mocked(logsApi.today).mockRejectedValueOnce(
      new ApiError(500, { code: 'INTERNAL_ERROR', message: 'boom', details: [] }),
    )
    vi.mocked(logsApi.today).mockResolvedValue(null)
    wrapper = mount(Harness, { attachTo: document.body })
    document.getElementById('open')?.click()
    await settle()
    expect(dialog().querySelector('[role="alert"]')).toBeTruthy()

    await press('Cancel')
    document.getElementById('open')?.click()
    await settle()

    expect(dialog().querySelector('[role="alert"]')).toBeNull()
    expect(statusText()).toBe('Step 1 of 4: Mood')
  })
})
