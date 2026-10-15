import { expect, test } from '../fixtures/csp'

test('recorrido crítico: test-login, dashboard, registrar ánimo y confirmar sin recargar', async ({ page }) => {
  // Contador de cargas de documento: sigue valiendo 1 si la SPA actualiza el
  // dashboard sin recargar la página entera tras guardar el registro.
  await page.addInitScript(() => {
    const w = window as unknown as { __mhtLoads?: number }
    w.__mhtLoads = (w.__mhtLoads ?? 0) + 1
  })

  // Email único por ejecución: el endpoint hace upsert por email y un usuario
  // nuevo garantiza que hoy no tiene registro, incluso si el test se repite.
  const email = `e2e-${Date.now()}@example.com`

  // El mismo browser context guarda la cookie de sesión que fija el backend,
  // así que el siguiente goto llega autenticado a /dashboard.
  const login = await page.request.post('/api/auth/test-login', {
    data: { email, name: 'Ana Gómez' },
  })
  expect(login.status()).toBe(204)

  await page.goto('/dashboard')

  await expect(page.getByRole('heading', { name: 'Hi, Ana.' })).toBeVisible()
  await expect(page.getByText('How are you feeling today? Take a minute to log it.')).toBeVisible()
  await expect(page.getByText('No data yet')).toBeVisible()

  await page.getByRole('button', { name: 'Log my day' }).click()

  const dialog = page.getByRole('dialog', { name: 'Daily log' })
  await expect(dialog).toBeVisible()

  await dialog.getByRole('radio', { name: 'Very good' }).click()
  await dialog.getByRole('button', { name: 'Save now' }).click()

  await expect(page.getByText('Thanks for logging today!')).toBeVisible()
  await expect(
    page.getByText("You already logged how you're feeling today. You can adjust it if something changed."),
  ).toBeVisible()

  await expect(page.getByText('No data yet')).toBeHidden()
  await expect(page.locator('canvas')).toBeVisible()

  const monthButton = page.getByRole('button', { name: 'Month' })
  await monthButton.click()
  await expect(monthButton).toHaveAttribute('aria-pressed', 'true')

  expect(await page.evaluate(() => (window as unknown as { __mhtLoads: number }).__mhtLoads)).toBe(1)
})

// El día lo decide el cliente. A las 20:00 en Bogotá (UTC-5) ya es el día siguiente en UTC;
// el modal debe consultar y guardar el día local, y al reabrirlo mostrar lo guardado.
// El reloj queda en una fecha pasada fija para que el servidor (que acota la fecha a
// "UTC + 1 día") la acepte siempre, y distinta del día UTC real: si el modal no enviara
// `?date=`, el servidor respondería con su propio día y el formulario reabriría vacío.
test.describe('día local en America/Bogota', () => {
  test.use({ timezoneId: 'America/Bogota' })

  test('guardar, cerrar y reabrir el modal muestra los valores guardados', async ({ page }) => {
    // 20:00 del 15 de marzo en Bogotá = 01:00 UTC del 16.
    await page.clock.install({ time: new Date('2026-03-16T01:00:00Z') })

    const email = `e2e-tz-${Date.now()}@example.com`
    const login = await page.request.post('/api/auth/test-login', {
      data: { email, name: 'Ana Gómez' },
    })
    expect(login.status()).toBe(204)

    const todayRequests: string[] = []
    page.on('request', (request) => {
      if (request.url().includes('/api/logs/today')) {
        todayRequests.push(new URL(request.url()).searchParams.get('date') ?? '')
      }
    })

    await page.goto('/dashboard')
    await page.getByRole('button', { name: 'Log my day' }).click()

    const dialog = page.getByRole('dialog', { name: 'Daily log' })
    await expect(dialog).toBeVisible()
    await dialog.getByRole('radio', { name: 'Very good' }).click()
    await dialog.getByRole('button', { name: 'Save now' }).click()

    await expect(page.getByText('Thanks for logging today!')).toBeVisible()
    await expect(dialog).toBeHidden()

    // Reabrir: ahora el panel ofrece ajustar la entrada de hoy.
    await page.getByRole('button', { name: "Adjust today's entry" }).click()
    const reopened = page.getByRole('dialog', { name: 'Daily log' })
    await expect(reopened).toBeVisible()
    await expect(reopened.getByRole('radio', { name: 'Very good' })).toHaveAttribute(
      'aria-checked',
      'true',
    )

    // Todas las consultas de "hoy" llevaron el día local, no el UTC (16).
    expect(todayRequests.length).toBeGreaterThan(0)
    expect(new Set(todayRequests)).toEqual(new Set(['2026-03-15']))
  })
})
