/**
 * Contrato de error de la API.
 *
 * Es transversal, no solo del registro diario: refleja `ErrorResponse` y `ErrorBody` y los códigos
 * que emite Core/Exceptions/GlobalExceptionHandler.cs, y cualquier respuesta 4xx o 5xx de
 * cualquier endpoint llega con esta forma. El resto del contrato (registros y usuario) está en
 * `daily-log.ts`.
 */

/**
 * Un fallo de validación por campo.
 *
 * Ojo: `field` llega en PascalCase ("MoodRating", "LogDate",
 * "Symptoms[0].Severity") mientras que todo lo demás en el cable es camelCase.
 * Es el nombre crudo de la propiedad de .NET que emite FluentValidation, así
 * que no lo pases por una función camelCase.
 */
export interface ApiErrorDetail {
  field: string
  message: string
}

/** Códigos emitidos por `GlobalExceptionHandler`. */
export type ApiErrorCode =
  'VALIDATION_ERROR' | 'UNAUTHORIZED' | 'NOT_FOUND' | 'RATE_LIMITED' | 'INTERNAL_ERROR'

/** Espejo de `ErrorBody`. */
export interface ApiErrorBody {
  code: ApiErrorCode
  message: string
  details: ApiErrorDetail[]
}

/**
 * Espejo de `ErrorResponse`. Es el cuerpo de cualquier respuesta 4xx y 5xx,
 * con la carga anidada bajo `error`:
 *
 *   { "error": { "code": "VALIDATION_ERROR", "message": "...",
 *                "details": [ { "field": "MoodRating", "message": "..." } ] } }
 */
export interface ApiErrorResponse {
  error: ApiErrorBody
}
