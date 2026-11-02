# Documentación

Índice de la documentación técnica del Mental Health Tracker. El `README.md` de la raíz cubre cómo
arrancar, configurar y probar el proyecto; aquí está el porqué.

## Por dónde empezar

| Si quieres… | Lee |
| --- | --- |
| Entender cómo encajan las piezas y cómo viajan los datos | [Estructura de módulos](architecture.md#estructura-de-módulos) |
| Saber **por qué** algo es como es | [ADR](adr/README.md) y las «Decisiones de diseño» de [`architecture.md`](architecture.md) |
| Escalar a varias réplicas | [Migraciones al arrancar](architecture.md#migraciones-aplicadas-al-arrancar), [Redis opcional](architecture.md#redis-opcional-backplane-de-signalr-y-rate-limiting-distribuido-scalingredis) y las sondas `live` / `ready` |
| Operar en producción (TLS, proxy, health, logs) | [Modelo de confianza en el proxy](architecture.md#modelo-de-confianza-en-el-proxy-la-api-no-se-expone-se-alcanza-a-través-de-nginx), [HTTPS y HSTS](architecture.md#https-y-hsts-responsabilidad-del-proxy-de-borde-no-de-la-api) y [Observabilidad](architecture.md#observabilidad) |
| Revisar la accesibilidad del frontend | [`accessibility.md`](accessibility.md) |

## Documentos

| Documento | Contenido |
| --- | --- |
| [`architecture.md`](architecture.md) | Arquitectura: estructura de módulos, decisiones de diseño y de datos, flujo de autenticación con Google, observabilidad y límites de alcance. Índice de secciones más abajo. |
| [`adr/`](adr/README.md) | Registros de decisiones (ADR) con plantilla: el razonamiento completo de las decisiones clave. |
| [`accessibility.md`](accessibility.md) | Auditoría y correcciones de accesibilidad del frontend. |
| [`screenshots/`](screenshots/) | Capturas que usa el `README.md` de la raíz. |

Otros README del repositorio: [raíz](../README.md) (arranque, variables de entorno, comandos, testing, CI),
[`frontend/`](../frontend/README.md), [`e2e/`](../e2e/README.md) y [`secrets/`](../secrets/README.md).

## Decisiones (ADR)

| Nº | Decisión | Estado |
| --- | --- | --- |
| [0001](adr/0001-el-cliente-decide-el-dia.md) | El cliente decide el día del registro; el servidor solo lo acota a «UTC + 1 día» | Aceptada |
| [0002](adr/0002-tiempo-real-de-mejor-esfuerzo.md) | El tiempo real es de mejor esfuerzo; la fuente de verdad es la API | Aceptada |
| [0003](adr/0003-confianza-en-el-proxy.md) | Confianza en el proxy: la API no se expone, se alcanza a través de nginx | Aceptada |
| [0004](adr/0004-opciones-del-formulario-1-a-1-con-los-enums.md) | Las opciones del formulario son 1:1 con los enums del backend | Aceptada |

Para añadir una, sigue [«Cómo añadir una»](adr/README.md#cómo-añadir-una).

## Índice de `architecture.md`

- [Estructura de módulos](architecture.md#estructura-de-módulos)
  - [Flujo de datos: guardar un registro](architecture.md#flujo-de-datos-guardar-un-registro)
- [Decisiones de diseño](architecture.md#decisiones-de-diseño)
  - [Sesión: JWT en cookie httpOnly (no en localStorage)](architecture.md#sesión-jwt-en-cookie-httponly-no-en-localstorage)
  - [CSRF y SameSite=Lax (sin token CSRF adicional)](architecture.md#csrf-y-samesitelax-sin-token-csrf-adicional)
  - [Sin refresh tokens ni token CSRF explícito (omisión consciente)](architecture.md#sin-refresh-tokens-ni-token-csrf-explícito-omisión-consciente)
  - [Canal de tiempo real (SignalR): misma cookie de sesión, sin segundo mecanismo de auth](architecture.md#canal-de-tiempo-real-signalr-misma-cookie-de-sesión-sin-segundo-mecanismo-de-auth)
  - [Notificación en tiempo real: de mejor esfuerzo, la fuente de verdad es la API](architecture.md#notificación-en-tiempo-real-de-mejor-esfuerzo-la-fuente-de-verdad-es-la-api)
  - [Escalado horizontal del canal en tiempo real: señalada limitación, sin backplane (decisión de alcance)](architecture.md#escalado-horizontal-del-canal-en-tiempo-real-señalada-limitación-sin-backplane-decisión-de-alcance)
  - [Rate limiting en memoria: mismo backplane pendiente que SignalR (decisión de alcance)](architecture.md#rate-limiting-en-memoria-mismo-backplane-pendiente-que-signalr-decisión-de-alcance)
  - [Redis opcional: backplane de SignalR y rate limiting distribuido (Scaling:Redis)](architecture.md#redis-opcional-backplane-de-signalr-y-rate-limiting-distribuido-scalingredis)
  - [Modelo de confianza en el proxy: la API no se expone, se alcanza a través de nginx](architecture.md#modelo-de-confianza-en-el-proxy-la-api-no-se-expone-se-alcanza-a-través-de-nginx)
  - [HTTPS y HSTS: responsabilidad del proxy de borde, no de la API](architecture.md#https-y-hsts-responsabilidad-del-proxy-de-borde-no-de-la-api)
  - [El día del registro lo decide el cliente: el servidor solo lo acota a "UTC + 1 día"](architecture.md#el-día-del-registro-lo-decide-el-cliente-el-servidor-solo-lo-acota-a-utc--1-día)
  - [Separación de capas: controller → service → repository](architecture.md#separación-de-capas-controller--service--repository)
  - [Base de datos de pruebas: docker-compose.test.yml, sin Testcontainers (decisión de alcance)](architecture.md#base-de-datos-de-pruebas-docker-composetestyml-sin-testcontainers-decisión-de-alcance)
  - [Opciones del formulario: 1:1 con los enums del backend](architecture.md#opciones-del-formulario-11-con-los-enums-del-backend)
  - [Tipos del contrato: espejo a mano y OpenAPI como detector de deriva, no como fuente](architecture.md#tipos-del-contrato-espejo-a-mano-y-openapi-como-detector-de-deriva-no-como-fuente)
- [Decisiones de datos](architecture.md#decisiones-de-datos)
  - [symptoms como jsonb y no una tabla hija normalizada](architecture.md#symptoms-como-jsonb-y-no-una-tabla-hija-normalizada)
  - [Escalas numéricas como smallint con el rango validado en la aplicación](architecture.md#escalas-numéricas-como-smallint-con-el-rango-validado-en-la-aplicación)
  - [Campos categóricos como text / text[] con el conjunto cerrado en la aplicación, no ENUM de Postgres](architecture.md#campos-categóricos-como-text--text-con-el-conjunto-cerrado-en-la-aplicación-no-enum-de-postgres)
- [Flujo de autenticación con Google (OAuth 2.0 + OpenID Connect)](architecture.md#flujo-de-autenticación-con-google-oauth-20--openid-connect)
  - [Recorrido paso a paso](architecture.md#recorrido-paso-a-paso)
  - [Paso 2–3: inicio del flujo y cookie state](architecture.md#paso-23-inicio-del-flujo-y-cookie-state)
  - [Paso 6: consumo de la cookie y anti-CSRF](architecture.md#paso-6-consumo-de-la-cookie-y-anti-csrf)
  - [Paso 7: intercambio servidor-a-servidor del code](architecture.md#paso-7-intercambio-servidor-a-servidor-del-code)
  - [Paso 8: validación del id_token](architecture.md#paso-8-validación-del-id_token)
  - [Paso 9: upsert por sub](architecture.md#paso-9-upsert-por-sub)
  - [Paso 10: emisión de la sesión](architecture.md#paso-10-emisión-de-la-sesión)
  - [Notas de borde](architecture.md#notas-de-borde)
- [Observabilidad](architecture.md#observabilidad)
  - [Correlación por X-Request-Id](architecture.md#correlación-por-x-request-id)
  - [Propiedades estructuradas por las que filtrar](architecture.md#propiedades-estructuradas-por-las-que-filtrar)
  - [Redacción de cookies y cabeceras de autorización](architecture.md#redacción-de-cookies-y-cabeceras-de-autorización)
  - [GET /api/health consulta la base (SELECT 1), no devuelve un literal](architecture.md#get-apihealth-consulta-la-base-select-1-no-devuelve-un-literal)
- [Límites conocidos de alcance](architecture.md#límites-conocidos-de-alcance)
  - [SignalR sin backplane](architecture.md#signalr-sin-backplane)
  - [Rate limiting en memoria](architecture.md#rate-limiting-en-memoria)
  - [Migraciones aplicadas al arrancar](architecture.md#migraciones-aplicadas-al-arrancar)
- [De Node/React a .NET/Vue](architecture.md#de-nodereact-a-netvue)
  - [Tabla de equivalencias](architecture.md#tabla-de-equivalencias)
  - [Inyección de dependencias del framework](architecture.md#inyección-de-dependencias-del-framework)
  - [FluentValidation en lugar de zod](architecture.md#fluentvalidation-en-lugar-de-zod)
  - [Grupos de SignalR en lugar de rooms de Socket.IO](architecture.md#grupos-de-signalr-en-lugar-de-rooms-de-socketio)
  - [Lo que no cambió](architecture.md#lo-que-no-cambió)
