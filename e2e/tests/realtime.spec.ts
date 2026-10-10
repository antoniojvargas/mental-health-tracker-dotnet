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

  // Esperar el handshake de SignalR en B. Cuando el servidor lo responde, B ya
  // está en el grupo del usuario (LogsHub.OnConnectedAsync) y ninguna emisión de
  // A se pierde. El listener se cuelga en cuanto se crea el socket del hub — vía
  // el evento 'websocket' de la página, no tras un `await`— porque el frame del
  // handshake puede emitirse antes de que un listener posterior se adjunte, y
  // Playwright no reproduce frames ya recibidos. El filtro descarta el socket de
  // HMR de Vite, que no pasa por /hub.
  let markReady!: () => void
  const hubReady = new Promise<void>((resolve) => {
    markReady = resolve
  })
  pageB.on('websocket', (socket) => {
    if (!socket.url().includes('/hub')) return
    socket.once('framereceived', () => markReady())
  })

  await pageB.goto('/dashboard')
  await expect(pageB.getByRole('heading', { name: 'Hi, Ana.' })).toBeVisible()
  await expect(pageB.getByText('How are you feeling today? Take a minute to log it.')).toBeVisible()

  await hubReady

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