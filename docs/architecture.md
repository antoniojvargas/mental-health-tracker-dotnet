# Arquitectura

Documento de arquitectura del Mental Health Tracker.

## Decisiones de diseño

### Sesión: JWT en cookie `httpOnly` (no en `localStorage`)

El token de sesión (JWT) se transporta y almacena en una cookie marcada como `httpOnly`,
en lugar de guardarlo en `localStorage` del navegador.

**Mecanismo de acceso al token**

| Almacenamiento    | Legible por scripts de la página | Vía de autenticación |
| ----------------- | -------------------------------- | -------------------- |
| `localStorage`    | Sí (XSS lo compromete)          | Header `Authorization` |
| Cookie `httpOnly` | No                              | Cookie enviada automáticamente |

**Motivos**

1. `localStorage` es legible por cualquier script que se ejecute en la página. Un
   ataque XSS permite extraer el JWT y suplantar al paciente. Al vivir en una cookie
   `httpOnly`, el token queda fuera del alcance de JavaScript.
2. Los datos protegidos son autorreportes de salud mental, una categoría
   especialmente sensible. La exposición de una sesión permitiría acceder al historial
   completo del paciente, por lo que se aplica el criterio de máxima restricción de
   exposición del token.
3. El flujo OAuth con Google es una navegación completa del navegador, no un `fetch`
   desde JavaScript. El callback (`redirect_uri`) ejecuta un `302` en el navegador y
   el backend no puede devolver un payload JSON desde ahí; la única vía que tiene para
   entregar la sesión es una cookie (vía `Set-Cookie`) acompañada de un redirect hacia
   el frontend.

**Implicaciones técnicas**

- Las cookies de autenticación se escriben con opciones centralizadas (`AuthCookieOptions`):
  `HttpOnly`, `SameSite=Lax`, `Path=/` y `Secure` solo cuando el entorno es `Production`
  (en desarrollo local sobre HTTP un cookie `Secure` no se almacena). Ver
  "CSRF y SameSite=Lax" abajo.
- El frontend no administra el token: la sesión queda asociada a la cookie y el
  backend autentica cada petición a partir de ella.
- El cierre de sesión se realiza invalidando/borrando la cookie desde el backend.

### CSRF y SameSite=Lax (sin token CSRF adicional)

Las mutaciones de este alcance (guardado de registros diarios, cierre de sesión, etc.)
se envían con `SameSite=Lax` y **no requieren un token CSRF adicional**. Motivos:

1. Una cookie `SameSite=Lax` no se incluye en peticiones cross-site: solo viaja en
   navegaciones a nivel de página (`top-level`) con `GET`. Un `<form>` o un `fetch`
   lanzado desde otro sitio (un origen distinto del site de la app) no adjunta la
   cookie de sesión, por lo que la mutación forjada llega sin autenticar y se rechaza.
   Lax permite los `GET` de navegación, que es exactamente lo que necesita el redirect
   del callback OAuth hacia el frontend.
2. En este despliegue frontend y API comparten `site` (mismo registrable domain; en
   desarrollo ambos son `localhost`). Eso es lo que permite que la SPA llame a la API
   con `credentials` llevando la cookie. Un atacante externo pertenece a otro `site`,
   nunca al de la app, así que no puede conseguir que el navegador adjunte la cookie a
   una petición creada desde su página.
3. El único vector bajo el cual un atacante podría disparar una mutación con sesión
   válida sería que un script del *propio* site (mismo `site`) la genere: eso es XSS,
   no CSRF, y se mitiga con controles distintos que sí tenemos (cookie `HttpOnly` que
   oculta el token a JS y CSP `default-src 'self'`).
4. El CSRF propio del flujo OAuth "login CSRF" no lo cubre SameSite (Google no envía
   nuestras cookies); se cubre con el parámetro `state` aleatorio firmado/almacenado en
   cookie y comparado en el callback.

**Limitación**: esta decisión asume frontend y API en el mismo `site`. Si en el futuro
se sirvieran desde sitios distintos (p. ej. `app.com` y `api.app.com`), la cookie
`SameSite=Lax` no llegaría en las llamadas `fetch` de la SPA y habría que migrar a
`SameSite=None; Secure` o a un header de autorización, y revisar esta conclusión de CSRF.

### Sin refresh tokens ni token CSRF explícito (omisión consciente)

El sistema **no implementa refresh tokens ni un token CSRF explícito**. Es una omisión
consciente, no un pendiente:

1. **Access token de 7 días como única credencial de sesión**. El JWT se emite con
   `SessionCookieMaxAge` de 7 días en la cookie `httpOnly`. Al expirar, el usuario
   vuelve a pasar por el flujo OAuth de Google (que re-presenta la pantalla de
   consentimiento si es necesario). No hay rotación de tokens ni un segundo token de
   larga duración que renovarlo.
2. **Sin tabla de tokens ni revocación**. No existe una tabla de sesiones/refresh tokens
   que persista, rote o revoque credenciales. La sesión es puramente stateless: vive solo
   en la firma del JWT y en la cookie. El cierre de sesión borra la cookie en el cliente;
   no invalida un token en el servidor.
3. **CSRF cubierto por `SameSite=Lax`**. La omisión del token CSRF explícito se justifica
   en la sección anterior: con frontend y API en el mismo `site`, la cookie `SameSite=Lax`
   ya bloquea las peticiones cross-site, que es el vector que un token CSRF mitiga.

**Por qué esta omisión completa el alcance**: para un producto de autorreporte personal
con un flujo de login mediado por Google, agregar rotación/revocación implicaría una
infraestructura de sesiones (tabla de tokens, jobs de limpieza, endpoints de refresh)
que hoy no aporta protección adicional proporcional al riesgo: el mismo `state` del OAuth
y el `SameSite=Lax` ya cubren los ataques de login-CSRF y CSRF clásico, y el acceso
indefinido post-logout no es alcanzable sin que el atacante posea previamente la cookie
`httpOnly` (que es inaccesible a JavaScript).

**Cuándo reconsiderar**: si la app necesitara el usuario "siempre autenticado" más allá de
7 días sin re-consentimiento (p. ej. apps móviles o sesiones de clientes longevas), o si
el alcance creciera a operaciones administrativas, se debería introducir refresh tokens
con rotación y revocación server-side.

### Canal de tiempo real (SignalR): misma cookie de sesión, sin segundo mecanismo de auth

El canal de tiempo real (`/hub/logs`, hub SignalR `LogsHub`) se autentica con la **misma
cookie de sesión `access_token`** que el resto de la API, verificada durante el handshake
de la conexión con `JwtService`. **No existe un segundo mecanismo de autenticación para el
canal de tiempo real.**

**Mecanismo**

| Capa                     | Detalle                                                                                         |
| ------------------------ | ----------------------------------------------------------------------------------------------- |
| Atributo `[RequireAuth]` | `LogsHub` lo declara igual que los controllers; SignalR lo propaga a los metadatos del endpoint |
| Middleware               | `RequireAuthMiddleware` lo detecta y valida la cookie `access_token` con `JwtService`           |
| Handshake                | La validación corre en el *negotiate* y en el *upgrade* a WebSocket: sin token válido la conexión se rechaza con `401` |

**Motivos**

1. **Un solo token de sesión para toda la app.** El mismo JWT `httpOnly` que autentica los
   endpoints HTTP autentica la conexión en tiempo real. El frontend no necesita gestionar un
   fixture de credenciales extra (ni `accessTokenFactory`, ni queries con token) y no se
   introduce superficie de ataque nueva para transportar un segundo secreto.
2. **SameSite=Lax no bloquea el WebSocket.** La cookie viaja en el handshake porque es una
   navegación same-site (frontend y API en el mismo `site`), igual que en el resto del flujo
   HTTP; la conclusión de CSRF de arriba se mantiene intacta.

**Implicaciones técnicas**

- Los hubs declaran `[RequireAuth]` y se benefician del mismo pipeline que los controllers:
  una sola ruta de verificación, testeo y transporte de claims hacia `context.User`.
- Si no hay cookie o el JWT no verifica (caducado, alterado o firmado mal), el handshake
  devuelve `401` y el cliente jamás establece la conexión: no hay "conexión anónima" con
  autorización diferida por hub.

### Escalado horizontal del canal en tiempo real: señalada limitación, sin backplane (decisión de alcance)

El canal en tiempo real **no está preparado para correr en varias réplicas de la API**:
no hay backplane (p. ej. Azure SignalR Service o Redis) detrás de SignalR. Su estado de
grupos y conexiones vive **en memoria de cada instancia**. Es una **decisión consciente de
alcance**, no un olvido.

**Qué implica**

| Escenario                           | Comportamiento actual                                                   |
| ----------------------------------- | ----------------------------------------------------------------------- |
| Una sola réplica                    | Los grupos `user:{userId}` y el broadcast por conexión funcionan porque todo vive en la misma memoria |
| Varias réplicas sin backplane       | Un usuario conectado a la instancia A recibe el evento de una escritura de su propio log solo si esa escritura también la procesó A; si la procesa la instancia B, el evento no llega porque `user:{userId}` no existe en la memoria de B |

**Motivos para aceptar la limitación hoy**

1. **El estado real sigue siendo correcto.** La falla es solo de *entrega inmediata* del
   evento: el log quedó persistido por B y un refresco HTTP (`GET /api/logs`) devuelve el
   dato actualizado venga de la réplica que venga. No hay pérdida de datos ni divergencia.
2. **Una sola instancia es el despliegue actual.** El componente API se ejecuta como un
   único contenedor (ver `docker-compose.yml`); no hay load balancer ni autoscaling que
   justifique el costo operativo de un backplane hoy.
3. **SameSite=Lax y WebSockets encajan con un backend único.** El patrón de sesión por
   cookie y conexiones persistentes asume un `site` y un backend; un backplane solo tendría
   sentido cuando el escalado cuantitativo lo exija.

**Cuándo y cómo se resolvería**

Si la app creciera a varias réplicas, la mitigación directa es introducir un backplane
SignalR (Redis o Azure SignalR Service) registrado en `AddSignalR()`; los grupos
`user:{userId}` se sincronizarían entre instancias y el escenario "usuario en A, escritura
en B" volvería a entregar el evento. El diseño actual no impide este cambio: el emisor
(`ILogEventEmitter`) no conoce el transporte, así que el backplane sería un detalle de la
implementación en infraestructura.

### Separación de capas: controller → service → repository

La API se organiza en tres capas con una responsabilidad estricta por capa:

| Capa         | Responsabilidad                                                                 |
| ------------ | ------------------------------------------------------------------------------- |
| Controller   | Única capa que conoce HTTP: rutas, verbos, `IActionResult`, status codes y `[RequireAuth]`. Extrae el `userId` de los claims del principio de autenticación; nunca lo acepta del cuerpo ni del query. |
| Service      | Orquesta casos de uso (mapeo request→entidad, invocación al repositorio, mapeo entidad→DTO). No conoce HTTP: no devuelve DTOs con status codes ni lanza errores atados a un código HTTP. Solo puede fallar lanzando excepciones de dominio (`MentalHealthTracker.Domain.Errors`), que el manejador global traduce. |
| Repository   | Única capa que toca EF Core / ADO.NET. Expone operaciones de persistencia atómicas y aisladas de la vía HTTP. |

**Flujo típico**: `DailyLogController` (status codes + claims) → `DailyLogService`
(mapeo y orquestación) → `IDailyLogRepository` (único con `AppDbContext`). La validación
FluentValidation se ejecuta como filtro global antes del controller y entrega sus errores
en el mismo cuerpo que el manejador global, sin que el controller maneje validación.

**Por qué composición explícita y no un framework que la imponga por convención**

1. **Explícito vs. mágico**: soluciones como vertical slices con scaffolding o frameworks
   tipo *feature-first* (MediatR, handlers con atributos) imponen el flujo por convención
   y localización (carpetas, nombres, registros en un bus). El rastro controller → service
   → repository queda diseminado. Aquí la transición es visible en el código llamada a
   llamada y se puede auditar con una lectura lineal de cada módulo.
2. **Dependencias mínimas y reversibles**: la composición explícita no exige inyectar un
   contenedor de mensajes ni una librería de atributos; son interfaces C# corrientes
   (`IDailyLogService`, `IDailyLogRepository`) con registros DI verbosos. Se puede
   reemplazar una capa de forma aislada (p. ej. cambiar EF Core por ADO.NET) sin reescribir
   el framework del pipeline.
3. **Excepciones de dominio como único contrato de error**: al mantener el servicio libre
   de HTTP, el mismo caso de uso es reutilizable si en el futuro hay un cliente distinto
   (CLI, jobs, e2e). Un framework que enruta por convención tiende a pegar la lógica a
   `HttpContext` o al resultado de acción.
4. **Coste bajo para este alcance**: con tres módulos y un puñado de endpoints, el
   "andamiaje" de un framework de request pipelines superaría el tamaño del dominio.
   La regla "1 capa = 1 responsabilidad" se cumple sin frameworks.

**Implicaciones técnicas**

- Solo los controllers declaran `[RequireAuth]` y leen claims; servicios y repositorios
  reciben el `userId` como parámetro y no confían en el contexto HTTP.
- Los repositorios exponen operaciones atómicas (p. ej. el `ON CONFLICT ... DO UPDATE`
  con flag `was_inserted` de `DailyLogRepository`), de modo que el servicio no tenga que
  decidir entre insert/update leyendo primero: la carrera se resuelve en la base.
- FluentValidation se ejecuta como filtro de acción global (`FluentValidationActionFilter`)
  y traduce fallos a `ValidationException`; el manejador global produce el cuerpo
  `{ error: { code: "VALIDATION_ERROR", ... } }`. Ninguna capa propaga errores HTTP a mano.

**Cuándo reconsiderar**: si el número de módulos creciera hasta el punto de que la
inyección manual de dependencias por módulo se vuelva ruido, o si aparecieran
proyecciones/reads complejos que no son "un caso de uso por endpoint", podría evaluarse
un bus de comandos (MediatR) o CQRS — manteniendo siempre esta regla: el controller es lo
único que conoce HTTP y el repositorio lo único que toca la infraestructura de datos.