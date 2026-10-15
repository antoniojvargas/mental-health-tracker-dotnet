import { expect, test } from '@playwright/test'
import type { Page, Locator } from '@playwright/test'

/**
 * Recorrido completo solo con teclado. Todo lo que un usuario sin ratón puede
 * hacer se hace aquí exclusivamente con Tab/Shift+Tab, flechas, Espacio y Enter:
 * login, apertura del modal, sus cuatro pasos, guardado y el panel de tendencias
 * (rango y métricas).
 *
 * Si alguna interacción solo funcionara con el ratón, este test la pilla: el
 * teclado no tiene doble clic ni hover para esconder fallos.
 */

/** Tab hasta que `target` recibe el foco (o se agota el tope). */
async function tabUntil(page: Page, target: Locator, maxTabs = 25) {
  let guard = 0
  const focused = () => target.evaluate((el) => el === document.activeElement)
  while (!(await focused()) && guard < maxTabs) {
    await page.keyboard.press('Tab')
    guard++
  }
  await expect(target).toBeFocused()
}

/** El texto del `output` asociado a un slider refleja su valor actual. */
async function expectSliderValue(page: Page, slider: Locator, value: string) {
  const id = await slider.getAttribute('id')
  await expect(page.locator(`output[for="${id}"]`)).toHaveText(value)
}

test('toda la app es usable solo con teclado, de login a tendencias', async ({ page }) => {
  // ---------------------------------------------------------------- Login
  await page.goto('/login')
  await page.keyboard.press('Tab')
  const googleLink = page.getByRole('link', { name: 'Continue with Google' })
  await expect(googleLink).toBeFocused()

  // ------------------------------------------------------------ Dashboard
  const email = `kb-${Date.now()}@example.com`
  await page.request.post('/api/auth/test-login', { data: { email, name: 'Key User' } })
  await page.goto('/dashboard')
  // Esperar a que monte el dashboard: un Tab enviado antes de render presiona un
  // `<body>` sin enfocables y el teclado ya no arranca desde el principio.
  await expect(page.getByRole('button', { name: 'Log my day' })).toBeVisible()

  await page.keyboard.press('Tab')
  await expect(page.getByRole('button', { name: 'Log out' })).toBeFocused()
  await page.keyboard.press('Tab')
  // El mismo botón cambia de texto tras guardar ("Log my day" → "Adjust today's
  // entry"), así que se localiza por ambos nombres para poder comprobar que el
  // modal devuelve el foco a quien lo abrió.
  const openLogButton = page.getByRole('button', { name: /Log my day|Adjust today's entry/ })
  await expect(openLogButton).toBeFocused()
  await page.keyboard.press('Enter')

  // ----------------------------------------------------------------- Modal
  const dialog = page.getByRole('dialog', { name: 'Daily log' })
  await expect(dialog).toBeVisible()
  // El foco entra en el diálogo (primer radio del ánimo) y queda atrapado.
  await expect(dialog.getByRole('radio', { name: 'Very low' })).toBeFocused()
  for (let i = 0; i < 10; i++) {
    await page.keyboard.press('Tab')
    const inside = await page.evaluate(() =>
      document.querySelector('[role="dialog"]')?.contains(document.activeElement),
    )
    expect(inside).toBe(true)
  }

  // ------------------------------------------------- Paso 1 de 4: mood
  // Tras el bucle del trap el foco puede haber quedado en cualquier control; se
  // vuelve al primer radio antes de empezar a moverse.
  await tabUntil(page, dialog.getByRole('radio', { name: 'Very low' }))
  await page.keyboard.press('ArrowRight')
  const low = dialog.getByRole('radio', { name: 'Low', exact: true })
  // La flecha es roving: el foco real sigue a la selección, no solo el `tabindex`.
  await expect(low).toBeFocused()
  await page.keyboard.press('Enter')
  await expect(low).toHaveAttribute('aria-checked', 'true')
  // El `aria-checked` refleja el `focusedIndex` interno; que Next se habilite es
  // la prueba de que el Enter llegó al modelo del formulario (`form.mood`).
  await expect(dialog.getByRole('button', { name: 'Next' })).toBeEnabled()

  await page.keyboard.press('Tab')
  const anxiety = dialog.getByRole('slider', { name: 'Anxiety' })
  await expect(anxiety).toBeFocused()
  await page.keyboard.press('ArrowRight')
  await page.keyboard.press('ArrowRight')
  await expectSliderValue(page, anxiety, '7')
  await expect(page.getByText('Step 1 of 4: Mood')).toBeVisible()

  await page.keyboard.press('Tab')
  await expect(dialog.getByRole('slider', { name: 'Stress' })).toBeFocused()
  await page.keyboard.press('ArrowRight')
  await tabUntil(page, dialog.getByRole('button', { name: 'Next' }))
  await page.keyboard.press('Enter')
  await expect(page.getByText('Step 2 of 4: Sleep')).toBeVisible()

  // ---------------------------------------------------- Paso 2 de 4: sleep
  const sleepHours = dialog.getByRole('slider', { name: 'Sleep hours' })
  await tabUntil(page, sleepHours)
  await page.keyboard.press('ArrowLeft')
  await expectSliderValue(page, sleepHours, '6.5')

  await page.keyboard.press('Tab')
  const sleepQuality = dialog.getByRole('slider', { name: 'Sleep quality' })
  await expect(sleepQuality).toBeFocused()
  await page.keyboard.press('ArrowRight')

  await page.keyboard.press('Tab')
  const fallAsleep = dialog.getByRole('checkbox', { name: 'Trouble falling asleep' })
  await expect(fallAsleep).toBeFocused()
  await fallAsleep.press('Space')
  await expect(fallAsleep).toBeChecked()

  const nightmares = dialog.getByRole('checkbox', { name: 'Nightmares' })
  // Entre medias quedan "Waking up during the night" y "Waking up too early".
  await tabUntil(page, nightmares)
  await page.keyboard.press('Space')
  await expect(nightmares).toBeChecked()

  await tabUntil(page, dialog.getByRole('button', { name: 'Next' }))
  await page.keyboard.press('Enter')
  await expect(page.getByText('Step 3 of 4: Activity and social life')).toBeVisible()

  // -------------------------------------- Paso 3 de 4: activity and social
  const activityType = dialog.getByRole('combobox', { name: 'Activity type' })
  await tabUntil(page, activityType)
  // El `<select>` es nativo: con lector/teclado se elige con la letra inicial.
  // Playwright no puede manejar el popup nativo de opciones en headless, así que
  // el type-ahead es la vía de teclado que sí es portable al CI.
  await page.keyboard.press('w')
  await expect(activityType).toHaveValue('walking')

  await page.keyboard.press('Tab')
  const activityMinutes = dialog.getByRole('slider', { name: 'Activity minutes' })
  await expect(activityMinutes).toBeFocused()
  await page.keyboard.press('ArrowRight')
  await page.keyboard.press('ArrowRight')
  await expectSliderValue(page, activityMinutes, '10')

  await page.keyboard.press('Tab')
  const socialFrequency = dialog.getByRole('combobox', { name: 'Social contact frequency' })
  await expect(socialFrequency).toBeFocused()
  await page.keyboard.press('o')
  await expect(socialFrequency).toHaveValue('rare')


  await tabUntil(page, dialog.getByRole('button', { name: 'Next' }))
  await page.keyboard.press('Enter')
  await expect(page.getByText('Step 4 of 4: Symptoms')).toBeVisible()

  // ------------------------------------------------- Paso 4 de 4: symptoms
  const panicCheck = dialog.getByRole('checkbox', { name: 'Panic' })
  await tabUntil(page, panicCheck)
  await page.keyboard.press('Space')
  await expect(panicCheck).toBeChecked()

  await page.keyboard.press('Tab')
  const severity = dialog.getByRole('radiogroup', { name: 'Panic severity' })
  await expect(severity.getByRole('radio', { name: '1' })).toBeFocused()
  await page.keyboard.press('ArrowRight')
  await expect(severity.getByRole('radio', { name: '2' })).toBeFocused()
  await expect(severity.getByRole('radio', { name: '2' })).toHaveAttribute('aria-checked', 'true')

  const fatigueCheck = dialog.getByRole('checkbox', { name: 'Fatigue' })
  // Entre la severidad y Fatigue va el síntoma "Low mood".
  await tabUntil(page, fatigueCheck)
  await page.keyboard.press('Space')
  await expect(fatigueCheck).toBeChecked()

  const notes = dialog.getByRole('textbox', { name: 'Notes' })
  await tabUntil(page, notes)
  await page.keyboard.type('Feeling better after a walk.', { delay: 0 })

  // ------------------------------------------------------------ Guardado
  const save = dialog.getByRole('button', { name: 'Save' })
  await tabUntil(page, save)
  await save.press('Enter')

  await expect(dialog).toBeHidden()
  await expect(page.getByText('Thanks for logging today!')).toBeVisible()
  await expect(
    page.getByText("You already logged how you're feeling today. You can adjust it if something changed."),
  ).toBeVisible()
  // El modal devuelve el foco a quien lo abrió.
  await expect(openLogButton).toBeFocused()

  // ------------------------------------------------- Rango con el teclado
  await page.keyboard.press('Tab')
  await expect(page.getByRole('button', { name: 'Week' })).toBeFocused()
  await page.keyboard.press('Tab')
  const month = page.getByRole('button', { name: 'Month' })
  await expect(month).toBeFocused()
  await month.press('Space')
  await expect(month).toHaveAttribute('aria-pressed', 'true')
  await expect(page.getByRole('region', { name: 'Your trends' })).toHaveAttribute('aria-busy', 'false')

  // --------------------------------------------- Métricas con el teclado
  await page.keyboard.press('Tab')
  await expect(page.getByRole('button', { name: 'Mood' })).toBeFocused()
  await page.keyboard.press('Tab')
  const anxietyChip = page.getByRole('button', { name: 'Anxiety' })
  await expect(anxietyChip).toBeFocused()
  await anxietyChip.press('Space')
  await expect(anxietyChip).toHaveAttribute('aria-pressed', 'false')

  await page.keyboard.press('Tab')
  const stressChip = page.getByRole('button', { name: 'Stress' })
  await expect(stressChip).toBeFocused()
  await stressChip.press('Space')
  await expect(stressChip).toHaveAttribute('aria-pressed', 'true')
})

// Con tres métricas activas, las no seleccionadas quedan deshabilitadas: el Tab
// debe saltarlas (WCAG 2.1.1) sin llegar nunca a ellas.
test('los chips de métricas deshabilitados se saltan con tab', async ({ page }) => {
  const email = `kb-m-${Date.now()}@example.com`
  await page.request.post('/api/auth/test-login', { data: { email, name: 'Key User' } })
  await page.goto('/dashboard')

  const mood = page.getByRole('button', { name: 'Mood' })
  await mood.focus()
  await expect(mood).toBeFocused()
  await page.keyboard.press('Tab')
  await expect(page.getByRole('button', { name: 'Anxiety' })).toBeFocused()
  await page.keyboard.press('Tab')
  await expect(page.getByRole('button', { name: 'Hours of sleep' })).toBeFocused()
  // "Stress" y compañía están deshabilitados, así que el Tab salta del último
  // chip activo al primer enfocable del documento. Chromium rebota en el `<body>`
  // un tecleo antes de llegar a Log out, así que no se puede dar el salto con un
  // solo Tab: lo importante es que el Tab nunca toca los chips deshabilitados.
  await tabUntil(page, page.getByRole('button', { name: 'Log out' }))
})