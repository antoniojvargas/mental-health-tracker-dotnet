/**
 * Contrato del dominio de registro diario.
 *
 * Este archivo es el ÚNICO punto donde el frontend describe el contrato con
 * la API. Cualquier tipo del dominio que viaje por HTTP se declara aquí y se
 * importa desde aquí; si aparece una forma local en un componente, está mal.
 *
 * Refleja los DTOs de backend/MentalHealthTracker.Api/Modules/DailyLog/Dtos/,
 * los enums de backend/MentalHealthTracker.Domain/Enums/ y el contrato de
 * error de Core/Exceptions/GlobalExceptionHandler.cs.
 *
 * Formato del cable (System.Text.Json con `JsonSerializerDefaults.Web`):
 *   - Propiedades en camelCase.
 *   - Enums como STRINGS en snake_case minúscula, no como números. Es el
 *     comportamiento de `SnakeCaseEnumJsonConverter`; ver esas constantes más
 *     abajo. El backend acepta mayúsculas al leer, pero emite siempre minúsculas.
 *   - `Guid` viaja como string.
 *   - `DateOnly` viaja como "yyyy-MM-dd".
 *   - Los enteros de C# (`short`, `int`) son `number` en JS.
 *
 * Excepción a camelCase: `details[].field` de un error de validación llega en
 * PascalCase. Está anotado donde se declara.
 */

/** "yyyy-MM-dd". El backend lo formatea así y no acepta formato libre. */
export type IsoDate = string

// ---------------------------------------------------------------------------
// Enums
//
// Cada uno se declara como tupla `as const` + tipo unión: el tipo da la
// seguridad en las asignaciones y la constante permite iterar en la UI
// (v-model, selectores, sliders) sin duplicar la lista de valores.
// ---------------------------------------------------------------------------

export const SLEEP_DISTURBANCES = [
  'none',
  'insomnia',
  'nightmares',
  'frequent_waking',
  'early_waking',
] as const
export type SleepDisturbance = (typeof SLEEP_DISTURBANCES)[number]

export const ACTIVITY_TYPES = [
  'none',
  'walking',
  'running',
  'gym',
  'yoga',
  'cycling',
  'sports',
  'other',
] as const
export type ActivityType = (typeof ACTIVITY_TYPES)[number]

export const SOCIAL_FREQUENCIES = [
  'none',
  'rare',
  'occasional',
  'frequent',
  'daily',
] as const
export type SocialFrequency = (typeof SOCIAL_FREQUENCIES)[number]

export const SYMPTOM_TYPES = [
  'low_mood',
  'hopelessness',
  'fatigue',
  'irritability',
  'panic',
  'restlessness',
  'concentration',
  'appetite_change',
] as const
export type SymptomType = (typeof SYMPTOM_TYPES)[number]

/** Value object `Symptom`, no un enum. */
export interface Symptom {
  type: SymptomType
  /** 1-5, validado por FluentValidation. */
  severity: number
}

// ---------------------------------------------------------------------------
// DailyLog
// ---------------------------------------------------------------------------

/** Espejo de `DailyLogResponse`. Lo que devuelve GET /api/logs y POST /api/logs. */
export interface DailyLog {
  id: string
  logDate: IsoDate
  /** 1-5. */
  moodRating: number
  /** 1-10. */
  anxietyLevel: number
  /** 1-10. */
  stressLevel: number
  /**
   * Ojo la asimetría: en la petición es `decimal` y aquí es `double`. Se
   * almacena con precisión (3,1), así que solo sobrevive un decimal.
   */
  sleepHours: number
  /** 1-5. */
  sleepQuality: number
  sleepDisturbances: SleepDisturbance[]
  activityType: ActivityType | null
  /** 0-600. */
  activityMinutes: number | null
  socialFrequency: SocialFrequency
  symptoms: Symptom[]
  /** Máximo 1000 caracteres. */
  notes: string | null
  /**
   * Opaque a propósito. Viene en formato "O" de .NET, con 7 dígitos de
   * fracción y offset "+00:00": "2026-09-13T17:18:21.1234567+00:00".
   * `new Date()` con ese string no es fiable entre navegadores, porque el
   * formato de fecha de ECMA-262 admite 3 dígitos de fracción. Formatea
   * para mostrar y no lo parsees a ciegas.
   */
  createdAt: string
  updatedAt: string
}

/**
 * Espejo de `CreateDailyLogRequest`, el cuerpo de POST /api/logs.
 *
 * Ojo: no hay `UpdateDailyLogRequest`. POST es un upsert sobre
 * (user_id, log_date) y devuelve 201 al crear, 200 al actualizar.
 */
export interface CreateDailyLogInput {
  /** "yyyy-MM-dd". No puede estar en el futuro. */
  logDate: IsoDate
  moodRating: number
  anxietyLevel: number
  stressLevel: number
  /** 0-24. */
  sleepHours: number
  sleepQuality: number
  /**
   * Envía `[]`, nunca lo omitas. Está declarado como no-nullable en el
   * registro de C# pero no hay regla NotNull, así que omitirlo produce un
   * null que la capa de persistencia acaba guardando como lista vacía.
   */
  sleepDisturbances: SleepDisturbance[]
  activityType: ActivityType | null
  activityMinutes: number | null
  socialFrequency: SocialFrequency
  /** Misma regla que sleepDisturbances: siempre `[]`. */
  symptoms: Symptom[]
  notes: string | null
}

// ---------------------------------------------------------------------------
// Listado
// ---------------------------------------------------------------------------

/** Espejo de `DailyLogListMeta`. */
export interface DailyLogListMeta {
  from: IsoDate
  to: IsoDate
  limit: number
  offset: number
  /**
   * Total de coincidencias ignorando limit y offset. Se devuelve en todas las
   * páginas, así que sirve para el paginador sin una llamada extra.
   */
  total: number
}

/** Espejo de `DailyLogListResponse`, el cuerpo de GET /api/logs. */
export interface DailyLogListResponse {
  data: DailyLog[]
  meta: DailyLogListMeta
}

/**
 * Parámetros de GET /api/logs. Espejo de `ListDailyLogsQuery`, que se enlaza
 * desde el query string.
 *
 * Si no mandas `from` ni `to`, el backend usa hoy-29 días hasta hoy. Los
 * topes los impone `ListDailyLogsQueryValidator`: limit como mucho 366, offset
 * no negativo, from <= to y rango total de 366 días como máximo.
 */
export interface ListDailyLogsQuery {
  from?: IsoDate
  to?: IsoDate
  /** Por defecto 100. */
  limit?: number
  /** Por defecto 0. */
  offset?: number
}

// ---------------------------------------------------------------------------
// Errores
//
// Contrato transversal, no solo de registro diario. Vive aquí porque este es
// el archivo que declara el contrato, pero no depende de nada de DailyLog, así
// que si crece conviene moverlo a src/types/api-error.ts.
// ---------------------------------------------------------------------------

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
  | 'VALIDATION_ERROR'
  | 'UNAUTHORIZED'
  | 'NOT_FOUND'
  | 'RATE_LIMITED'
  | 'INTERNAL_ERROR'

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

// ---------------------------------------------------------------------------
// User
// ---------------------------------------------------------------------------

/**
 * Lo que devuelve GET /api/auth/me.
 *
 * No existe una clase `UserResponse`: el controlador devuelve un objeto
 * anónimo con estas cuatro propiedades. `googleId`, `createdAt` y `updatedAt`
 * no se exponen al cliente. La sesión viaja en la cookie httpOnly
 * `access_token`, así que no hay token en ninguna respuesta.
 */
export interface User {
  id: string
  email: string
  name: string
  avatarUrl: string | null
}
