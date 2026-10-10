import { expect, test } from '@playwright/test'

test('el frontend sirve la página de login con el enlace de Google', { tag: '@smoke' }, async ({ page }) => {
  await page.goto('/login')

  const googleLink = page.getByRole('link', { name: 'Continue with Google' })
  await expect(googleLink).toBeVisible()
  await expect(googleLink).toHaveAttribute('href', '/api/auth/google')
})

test('GET /api/health responde ok', { tag: '@smoke' }, async ({ request }) => {
  const response = await request.get('/api/health')

  expect(response.status()).toBe(200)
  expect(await response.json()).toMatchObject({ status: 'ok' })
})

test('el endpoint del hub responde algo distinto de un 5xx', { tag: '@smoke' }, async ({ request }) => {
  const response = await request.get('/hub/logs')

  expect(response.status()).toBeLessThan(500)
})