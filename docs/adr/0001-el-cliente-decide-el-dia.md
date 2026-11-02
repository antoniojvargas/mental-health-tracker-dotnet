# ADR 0001: El cliente decide el día del registro; el servidor solo lo acota a «UTC + 1 día»

- **Estado:** Aceptada
- **Fecha:** 2026-10-12
- **Áreas:** backend, frontend

## Contexto

Un registro pertenece a un **día calendario** (`logDate`, `yyyy-MM-dd`), y ese día es el que
el usuario vive en su zona horaria, no el de la hora UTC del servidor. Entre la medianoche
local y la UTC los dos días difieren: a las 20:00 en Bogotá (UTC-5) ya es el día siguiente
en UTC, y a las 08:00 en Tokio (UTC+9) todavía es el anterior. Si el cliente guardaba con su
fecha local y consultaba con la del servidor, el formulario "de hoy" abría vacío (o con el
registro de otro día) y el siguiente guardado sobrescribía el registro equivocado.

## Decisión

| Pieza                                   | Qué hace con el día                                                                 |
| --------------------------------------- | ----------------------------------------------------------------------------------- |
| Frontend (`utils/date.ts`)              | **Única fuente del "hoy"**: `todayIso()` calcula el día en hora local. Lo usan el guardado (`logDate`), la consulta (`GET /api/logs/today?date=`) y la ventana del panel (`from`/`to`). |
| `POST /api/logs`                        | Guarda el `logDate` que envía el cliente. El upsert es por `(user_id, log_date)`.   |
| `GET /api/logs/today?date=yyyy-MM-dd`   | Devuelve el registro de **ese** día. Sin `date`, usa el día UTC del servidor (comportamiento anterior, para no romper a otros clientes). |
| Validadores del servidor                | Solo **acotan** la fecha: no puede superar en más de 1 día la fecha UTC actual (`CreateDailyLogValidator` y `TodayQueryValidator`). |

### Por qué el límite es «UTC + 1 día»

El servidor no conoce la zona del cliente y no debe fiarse de ella, así que no recalcula el
día: solo rechaza lo imposible. La zona más adelantada es UTC+14, donde ya es "mañana"
respecto al día UTC durante hasta 14 horas, de modo que ninguna fecha legítima supera la
fecha UTC + 1 día. Con `UTC + 1` se aceptan todos los clientes reales y se rechazan las
fechas claramente futuras (`UTC + 2` en adelante) con el mismo mensaje de antes,
`logDate cannot be in the future.`, y el sobre `VALIDATION_ERROR`. El pasado no se acota: se
puede registrar o corregir un día anterior.

## Alternativas descartadas

- **Que el servidor decida el día con la zona del usuario.** Exigiría guardar y fiarse de una
  zona por usuario (o recibirla en cada petición), y el viaje entre zonas la haría incorrecta.
  El cliente ya sabe qué día vive.
- **Seguir con el día UTC en todo.** Es lo que causaba el defecto: en cualquier zona distinta
  de UTC hay horas del día en las que el "hoy" del usuario no coincide con el del servidor.

## Consecuencias

- La fecha es un dato del cliente: un usuario puede registrar con un día que no es el suyo
  (dentro de la cota). No es un problema de seguridad: cada usuario solo escribe en sus propios
  registros y el campo describe su propio día.
- Un cliente que llame a `GET /api/logs/today` sin `date` obtiene el día UTC; el frontend
  siempre envía `date`.
- Los tests fijan la regla con relojes simulados: la utilidad de fechas se prueba en
  `Asia/Tokyo` y `America/Bogota`, el modal a las 20:00 de Bogotá (01:00 UTC del día
  siguiente) y un e2e con `timezoneId: 'America/Bogota'` que guarda, cierra y reabre el modal.

## Referencias

- `frontend/src/utils/date.ts` (`todayIso`, `toIsoDate`, `parseIsoDate`): única fuente del «hoy» del cliente.
- `CreateDailyLogValidator` y `TodayQueryValidator` (backend): la cota «UTC + 1 día».
- Resumen y contexto general en [`architecture.md`](../architecture.md#el-día-del-registro-lo-decide-el-cliente-el-servidor-solo-lo-acota-a-utc--1-día).
