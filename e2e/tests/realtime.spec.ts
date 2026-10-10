import { expect, test } from '@playwright/test'

test('tiempo real: guardar en una pestaña se refleja en la otra sin recargar ni volver a pedir datos', async ({
  context,
}) => {
  const email = `e2e-realtime-${Date.now()}@example.com`

  const login = await context.request.post('/api/auth/test-login', {
    data: { email, name: 'Ana Gómez' },
  })
  expect(login.status()).toBe(204)

  const pageA = await context.newPage()
  const pageB = await context.newPage()

  // La pestaña B no debe recargar ni volver a pedir la lista: el cambio tiene
  // que llegarle por el websocket del hub.
  await pageB.addInitScript(() => {
    const w = window as unknown as { __mhtLoads?: number }
    w.__mhtLoads = (w.__mhtLoads ?? 0) + 1
  })

  let getLogsRequests = 0
  pageB.on('request', (request) => {
    if (request.method() === 'GET' && new URL(request.url()).pathname === '/api/logs') {
      getLogsRequests += 1
    }
  })

  const pageBWebSocket = pageB.waitForEvent('websocket')

  await pageB.goto('/dashboard')
  await expect(pageB.getByRole('heading', { name: 'Hi, Ana.' })).toBeVisible()
  await expect(pageB.getByText('How are you feeling today? Take a minute to log it.')).toBeVisible()

  // Esperar a que la pestaña B tenga el socket de SignalR montado y en el grupo
  // del usuario antes de guardar en A, o la emisión se pierde.
  const websocket = await pageBWebSocket
  await websocket.waitForEvent('framesent', { timeout: 10_000 })
  await websocket.waitForEvent('framereceived', { timeout: 10_000 })

  const baselineFetchCount = getLogsRequests

  await pageA.goto('/dashboard')
  await expect(pageA.getByRole('heading', { name: 'Hi, Ana.' })).toBeVisible()
  await pageA.getByRole('button', { name: 'Log my day' }).click()

  const dialogA = pageA.getByRole('dialog', { name: 'Daily log' })
  await expect(dialogA).toBeVisible()
  await dialogA.getByRole('radio', { name: 'Very good' }).click()
  await dialogA.getByRole('button', { name: 'Save now' }).click()

  await expect(pageA.getByText('Thanks for logging today!')).toBeVisible()

  await expect(
    pageB.getByText("You already logged how you're feeling today. You can adjust it if something changed."),
  ).toBeVisible({ timeout: 10_000 })
  await expect(pageB.getByText('No data yet')).toBeHidden()

  expect(await pageB.evaluate(() => (window as unknown as { __mhtLoads: number }).__mhtLoads)).toBe(1)
  expect(getLogsRequests).toBe(baselineFetchCount)
})