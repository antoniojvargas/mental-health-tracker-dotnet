import { expect, test } from '@playwright/test'

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