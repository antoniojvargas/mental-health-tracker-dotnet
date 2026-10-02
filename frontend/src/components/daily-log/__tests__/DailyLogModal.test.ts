// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { nextTick, defineComponent, h, ref } from 'vue'
import DailyLogModal from '../DailyLogModal.vue'
import Toast from '../../ui/Toast.vue'

const createLog = vi.fn()
const getToday = vi.fn()
const toastSuccess = vi.fn()
const toastError = vi.fn()

vi.mock('../../../services/logs.api', () => ({
  create: createLog,
  today: getToday,
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
  const nextBtn = Array.from(document.querySelectorAll('button')).find((b) => b.textContent?.trim() === 'Next')
  nextBtn?.click()
}

function clickSaveNow() {
  const saveNow = Array.from(document.querySelectorAll('button')).find((b) => b.textContent?.trim() === 'Save now')
  saveNow?.click()
}

function selectMood(value: number) {
  const moods = Array.from(document.querySelectorAll('[role="radio"][aria-label="Mood"]'))
  const mood = moods.find((r) => r.textContent?.trim() === String(value))
  mood?.click()
}

describe('DailyLogModal', () => {
  beforeEach(() => {
    createLog.mockReset()
    getToday.mockReset()
    toastSuccess.mockReset()
    toastError.mockReset()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    wrapper?.unmount()
    document.body.innerHTML = ''
  })

  it('navigates between steps', async () => {
    getToday.mockResolvedValue(null)
    wrapper = mount(Harness, { attachTo: document.body })
    document.getElementById('open')?.click()
    await nextTick()
    await nextTick()

    expect(document.body.textContent).toContain('Step 1 of 4')
    expect(document.body.textContent).toContain('Mood')

    selectMood(4)
    await nextTick()
    clickNext()
    await nextTick()
    expect(document.body.textContent).toContain('Step 2 of 4')
    expect(document.body.textContent).toContain('Sleep')

    clickNext()
    await nextTick()
    expect(document.body.textContent).toContain('Step 3 of 4')
    expect(document.body.textContent).toContain('Activity and social life')

    clickNext()
    await nextTick()
    expect(document.body.textContent).toContain('Step 4 of 4')
    expect(document.body.textContent).toContain('Symptoms')
  })

  it('can save from first step', async () => {
    getToday.mockResolvedValue(null)
    createLog.mockResolvedValue({ id: '1' })
    wrapper = mount(Harness, { attachTo: document.body })
    document.getElementById('open')?.click()
    await nextTick()
    await nextTick()

    selectMood(5)
    await nextTick()
    clickSaveNow()
    await nextTick()
    await nextTick()

    expect(createLog).toHaveBeenCalled()
    expect(toastSuccess).toHaveBeenCalled()
  })

  it('sends expected payload on save', async () => {
    getToday.mockResolvedValue(null)
    createLog.mockResolvedValue({ id: '1' })
    wrapper = mount(Harness, { attachTo: document.body })
    document.getElementById('open')?.click()
    await nextTick()
    await nextTick()

    selectMood(3)
    clickSaveNow()
    await nextTick()
    await nextTick()

    expect(createLog).toHaveBeenCalledWith(
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
    getToday.mockResolvedValue(null)
    createLog.mockResolvedValue({ id: '1' })
    wrapper = mount(Harness, { attachTo: document.body })
    document.getElementById('open')?.click()
    await nextTick()
    await nextTick()

    selectMood(4)
    clickSaveNow()
    await nextTick()
    await nextTick()

    expect(toastSuccess).toHaveBeenCalledWith(expect.stringContaining('Thanks'))
    expect(document.body.textContent).not.toContain('Step 1')
  })

  it('shows error without closing modal when API fails', async () => {
    getToday.mockResolvedValue(null)
    createLog.mockRejectedValue(new ApiError(400, 'VALIDATION_ERROR', 'Invalid'))
    wrapper = mount(Harness, { attachTo: document.body })
    document.getElementById('open')?.click()
    await nextTick()
    await nextTick()

    selectMood(2)
    clickSaveNow()
    await nextTick()
    await nextTick()

    expect(toastError).toHaveBeenCalled()
    expect(document.body.textContent).toContain('Step 1 of 4')
  })
})
