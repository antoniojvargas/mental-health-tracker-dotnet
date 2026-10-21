/**
 * Traduce un fallo de la API a un mensaje que se puede poner en pantalla.
 *
 * La API rellena `message` con lo que le convenga —el nombre crudo de una
 * propiedad de .NET, un detalle de validación, a veces rastro del servidor— y ese
 * texto NO debe llegar al usuario: aquí se ignora y se elige la frase por `code`.
 * La voz es la de la app (primera persona del plural, como la nota de privacidad
 * del login), sin jerga técnica.
 *
 * Cualquier fallo que no sea un `ApiError` —`fetch` sin red, una respuesta que no
 * parsea— cae en el mensaje de conexión, que es lo único honesto que se puede
 * decir sin conocer la causa.
 *
 * Es el ÚNICO traductor a propósito: si cada `catch` compone su propio texto,
 * dos pantallas acaban contando el mismo fallo de dos maneras y la coherencia se
 * pierde justo donde más se necesita.
 */

import { ApiError } from './api-client'
import type { ApiErrorCode } from '../types/api-error'

/** Frase por código, en la voz de la app. */
const BY_CODE: Record<ApiErrorCode, string> = {
  VALIDATION_ERROR: 'Some entries need another look. Please review and try again.',
  RATE_LIMITED: "We're getting a lot of requests right now. Please try again in a moment.",
  UNAUTHORIZED: 'Your session has ended. Please sign in again.',
  NOT_FOUND: "We couldn't find what you were looking for.",
  INTERNAL_ERROR: 'Something went wrong on our end. Please try again.',
}

/** Lo que se dice cuando no se sabe la causa (sin conexión, respuesta ilegible). */
const OFFLINE = "We couldn't reach the server. Please check your connection and try again."

export function describeError(error: unknown): string {
  if (error instanceof ApiError) {
    // `?? OFFLINE` cubre un `code` que el backend estrene antes que este cliente.
    return BY_CODE[error.code] ?? OFFLINE
  }
  return OFFLINE
}
