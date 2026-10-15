import { describe, expect, it } from 'vitest'

import { ApiError } from './api-client'
import { describeError } from './error-messages'
import type { ApiErrorCode } from '../types/daily-log'

function apiError(code: string, message = 'texto crudo del servidor', status = 400): ApiError {
  return new ApiError(status, { code: code as ApiErrorCode, message, details: [] })
}

const OFFLINE = "We couldn't reach the server. Please check your connection and try again."

describe('describeError', () => {
  it.each([
    ['VALIDATION_ERROR', 'Some entries need another look. Please review and try again.'],
    ['RATE_LIMITED', "We're getting a lot of requests right now. Please try again in a moment."],
    ['UNAUTHORIZED', 'Your session has ended. Please sign in again.'],
    ['NOT_FOUND', "We couldn't find what you were looking for."],
    ['INTERNAL_ERROR', 'Something went wrong on our end. Please try again.'],
  ] as const)('traduce %s a su frase', (code, phrase) => {
    expect(describeError(apiError(code))).toBe(phrase)
  })

  it('nunca deja pasar el message de la API', () => {
    const message = 'System.NullReferenceException at Foo.Bar()'

    for (const code of [
      'VALIDATION_ERROR',
      'RATE_LIMITED',
      'UNAUTHORIZED',
      'NOT_FOUND',
      'INTERNAL_ERROR',
    ]) {
      expect(describeError(apiError(code, message))).not.toContain('NullReference')
    }
  })

  it('elige por code y no por status', () => {
    expect(describeError(apiError('NOT_FOUND', 'x', 500))).toBe(
      "We couldn't find what you were looking for.",
    )
  })

  it('un code desconocido (un backend más nuevo que el cliente) cae en el mensaje de conexión', () => {
    expect(describeError(apiError('SOMETHING_NEW'))).toBe(OFFLINE)
  })

  it.each([
    ['un TypeError de fetch sin red', new TypeError('Failed to fetch')],
    ['un Error cualquiera', new Error('boom')],
    ['un SyntaxError al parsear', new SyntaxError('Unexpected token')],
    ['un string', 'algo falló'],
    ['null', null],
    ['undefined', undefined],
    ['un objeto con forma de ApiError pero que no lo es', { status: 500, code: 'INTERNAL_ERROR' }],
  ])('un error que no es ApiError (%s) dice el mensaje de conexión', (_name, thrown) => {
    expect(describeError(thrown)).toBe(OFFLINE)
  })
})
