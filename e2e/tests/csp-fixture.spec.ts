import { expect, test } from '../fixtures/csp'

// Comprueba el propio fixture: una pagina con una CSP que bloquea su script en linea
// provoca un mensaje de consola de violacion, y el fixture debe hacer fallar el test.
// `test.fail()` invierte el resultado: este test pasa SOLO si el fixture lo hace fallar.
test('el fixture falla el test ante una violacion de CSP', async ({ page }) => {
  test.fail()

  await page.setContent(
    `<meta http-equiv="Content-Security-Policy" content="script-src 'none'">
     <script>window.ran = true</script>`,
  )

  expect(await page.evaluate(() => (window as unknown as { ran?: boolean }).ran)).toBeUndefined()
})

test('sin violaciones el fixture no interfiere', async ({ page }) => {
  await page.setContent('<p>hola</p>')
  await expect(page.getByText('hola')).toBeVisible()
})
