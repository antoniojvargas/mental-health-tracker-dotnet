# ADR 0004: Las opciones del formulario son 1:1 con los enums del backend

- **Estado:** Aceptada
- **Fecha:** 2026-10-13
- **Áreas:** frontend

## Contexto

El modal de registro diario traducía lo que el usuario elegía a los enums del backend (y al revés al
reabrir un registro) con funciones de mapeo propias, y las opciones de los selectores no coincidían
con los valores del enum. Esos mapeos perdían o alteraban datos sin avisar (hallazgo A5 del Bloque A):

- **Frecuencia social:** el mapeo inverso producía valores sin opción en el selector (`rare`,
  `occasional`, `frequent`).
- **Síntomas:** «Sleep» y «Energy» se enviaban ambos como `fatigue` (podía duplicar el síntoma), «Pain» se
  guardaba como `restlessness` y `hopelessness` se perdía al reabrir.
- **Alteraciones de sueño:** varias se guardaban como `none`.
- **Sliders «Activity level» y «Social connection»:** nunca se enviaban; el backend no tiene dónde guardarlos.

El resultado eran datos editados que se pierden o se duplican, sin ningún error visible.

## Decisión

Cada selector ofrece **exactamente** los valores del enum del backend y la clave de la opción **es** el
valor del enum:

- Las opciones viven en `components/daily-log/daily-log-options.ts`, derivadas de los tipos de
  `types/daily-log.ts` (espejo del contrato). Cada tabla de etiquetas es un `Record<Tipo, string>`: si el
  backend añade un valor al enum y se actualiza el contrato, TypeScript falla hasta que el valor tenga
  etiqueta, y falla igual si sobra una clave. El orden de las claves es el orden en pantalla.
- Se **eliminan** las funciones de mapeo (`mapSocialFrequency`, `mapSymptomType` y sus inversas): con las
  opciones 1:1 serían identidades. La conversión entre formulario y API queda en `daily-log-mapper.ts`
  (`toCreateDailyLogInput`, `fromDailyLog`), puro y sin pérdidas.
- `SleepDisturbance` se ofrece **sin `none`** («sin alteraciones» es no marcar ninguna casilla).
- Los síntomas son los 8 de `SymptomType`: Panic, Low mood, Fatigue, Irritability, Concentration,
  Appetite change, Restlessness, Hopelessness. `createDefaultSymptoms()` es la única fábrica de la lista.
- Se quitan los campos `activity` y `social` de `DailyLogForm` y sus sliders: pedir datos que se descartan
  es peor que no pedirlos.

## Alternativas descartadas

- **Seguir con etiquetas propias y mapear al enum:** es lo que producía las pérdidas. Cada mapeo es un
  sitio donde una tabla y el enum pueden divergir sin que nada lo avise.
- **Ampliar los enums del backend** para conservar las opciones que desaparecen («Restless legs»,
  «Snoring», «Interrupted sleep», «Housework» como valor propio…): es una funcionalidad nueva (migración,
  contrato, validación), no una corrección. Queda como alternativa si se quiere ese detalle.
- **Añadir `activity` y `social` como columnas nuevas:** igualmente funcionalidad; se dejó fuera.

## Consecuencias

- **Cambio visible:** desaparecen opciones que antes se guardaban como otra cosa («Restless legs»,
  «Snoring», «Other» e «Interrupted sleep» en sueño; «Housework» pasa a «Other»; «Sleep», «Energy» y
  «Pain» como síntomas), y los dos sliders del paso 3.
- **Un valor nuevo del backend no pasa desapercibido:** al actualizar `types/daily-log.ts`, el compilador
  (`vue-tsc`) señala la etiqueta que falta. El coste es mantener a mano ese espejo del contrato (ver
  «Tipos del contrato» en [`architecture.md`](../architecture.md)).
- **Idas y vueltas sin pérdida:** `daily-log-mapper.test.ts` comprueba, para cada valor de cada enum,
  que abrir un registro y guardarlo sin tocar nada envía los mismos campos; `daily-log-options.test.ts`
  y `SymptomPicker.test.ts` fijan las opciones.
- Los registros ya guardados con los valores que se perdían no se corrigen: el cambio evita perder datos
  de aquí en adelante.

## Referencias

- `frontend/src/components/daily-log/daily-log-options.ts`, `daily-log-mapper.ts` y `types/daily-log-form.ts`.
- Origen: hallazgo A5 del Bloque A (fase «Formulario de registro sin pérdida de datos»).
