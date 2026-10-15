import { expect, test as base } from '@playwright/test'

// Un navegador informa de una violacion de CSP con un mensaje de consola
// ("Refused to load/execute/connect ... because it violates the following Content
// Security Policy directive ..."), tanto en modo bloqueo como en Report-Only.
const CSP_VIOLATION = /Content[- ]Security[- ]Policy|violates the following|Refused to (apply|connect|execute|frame|load|navigate|send)/i

/**
 * `test` con un fixture automatico: cualquier mensaje de consola que sea una violacion
 * de CSP hace fallar el test, aunque la pagina siga funcionando. Una CSP que bloquea un
 * recurso a medias se ve como "todo bien" en las aserciones de UI y solo la consola lo
 * delata.
 */
export const test = base.extend<{ cspViolations: string[] }>({
  cspViolations: [
    async ({ page }, use) => {
      const violations: string[] = []
      page.on('console', (message) => {
        const text = message.text()
        if (CSP_VIOLATION.test(text)) violations.push(`[${message.type()}] ${text}`)
      })

      await use(violations)

      expect(violations, 'La consola informo de violaciones de CSP').toEqual([])
    },
    { auto: true },
  ],
})

export { expect }
