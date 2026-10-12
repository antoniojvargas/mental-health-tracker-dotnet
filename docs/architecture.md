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

### Rate limiting en memoria: mismo backplane pendiente que SignalR (decisión de alcance)

Las políticas de rate limiting (`auth`: 20 req/15 min por IP en `GET /api/auth/google/callback`;
`write`: 30 req/15 min por id de usuario en `POST /api/logs`) usan `AddRateLimiter` con
`FixedWindowRateLimiter`. El estado de la ventana (el contador de cada partición) vive
**en memoria de la instancia**: `System.Threading.RateLimiting` no persiste contadores en
ningún store compartido.

**Qué implica**

| Escenario                    | Comportamiento actual                                                                        |
| ---------------------------- | -------------------------------------------------------------------------------------------- |
| Una sola réplica             | El contador es local y exacto: la cuota se aplica de forma firme por IP o por id de usuario. |
| Varias réplicas sin store    | Cada instancia cuenta por separado, de modo que un cliente repartido entre réplicas puede consumir hasta una ventana por réplica; el límite deja de ser una **garantía global** y pasa a ser probabilístico. |

**Particiones por IP real detrás del proxy**

La política `auth` se particiona por IP de origen. En producción la API no recibe al cliente
directamente: nginx va delante y, sin más, `RemoteIpAddress` sería siempre la IP de nginx, de
modo que los 20 intentos por 15 min se compartirían entre **todos** los usuarios (un solo
atacante bloquearía el login de todos). Para evitarlo, con `ReverseProxy:Enabled=true`
`UseForwardedHeaders` es el **primer** middleware del pipeline (antes que el rate limiter) y
sustituye `RemoteIpAddress` por la IP de `X-Forwarded-For`, y el esquema por el de
`X-Forwarded-Proto`. Dos condiciones lo hacen seguro:

1. **Solo se aceptan las cabeceras de proxies declarados.** `ReverseProxy:KnownProxies` /
   `KnownNetworks` es la lista de confianza, y arrancar con `Enabled=true` y ambas vacías
   falla (confiar en cualquier origen permitiría falsificar la IP). En producción la lista es
   la subred fija `172.28.0.0/24` de la red de Compose, y el backend no publica puerto: solo
   nginx puede alcanzarlo desde ahí. Una petición que llegue de otra dirección conserva su IP
   real y su `X-Forwarded-For` se ignora.
2. **`ForwardLimit=1`: solo se lee la entrada más a la derecha.** nginx añade con
   `$proxy_add_x_forwarded_for` la IP que él ve (`$remote_addr`) **al final** de lo que el
   cliente haya enviado, así que un cliente puede mandar `X-Forwarded-For: 1.2.3.4` y llegará
   `1.2.3.4, <IP real>`. El middleware procesa la cabecera de derecha a izquierda y, con
   límite 1, consume únicamente la última entrada: la que puso nuestro nginx. Todo lo que el
   cliente escribió a la izquierda se descarta, y por tanto no puede inventarse otra IP ni
   repartir sus intentos entre IPs falsas (los tests de `ForwardedHeadersTests` y la prueba
   con la pila de producción lo confirman: 22 peticiones con IPs falsas distintas agotan la
   misma cuota).

El límite de 1 es correcto mientras nginx sea el único proxy de la cadena. Si se pusiera otro
delante (balanceador, CDN), `$remote_addr` de nginx pasaría a ser el de ese proxy: habría que
subir `ForwardLimit` a 2, añadir su red a `KnownNetworks` y configurar nginx para que confíe en
él (`real_ip`). Cambiar solo uno de los dos lados vuelve a compartir la cuota o a abrir la
puerta a IPs falsas.

La política `write` no cambia: se particiona por id de usuario, no por IP.

**Motivos para aceptar la limitación hoy**

1. **La mitigación real es la misma pieza de infraestructura que resolvería el backplane de
   SignalR: Redis.** El contador de ventana distribuido (p. ej. `INCR` + `EXPIRE` con una
   clave `rl:{ip}` / `rl:user:{id}`) y el backplane de grupos `user:{userId}` son la misma
   dependencia operativa. Tratar **ambas limitaciones como una sola decisión** evita añadir
   infraestructura por partida doble o escalar una réplica de SignalR y "olvidar" el rate
   limiting global (o al revés).
2. **El despliegue actual es una única réplica.** El componente API corre en un contenedor
   (ver `docker-compose.yml`); hoy el contador en memoria es exacto y no hay pérdida de
   comportamiento frente al objetivo declarado.
3. **Best-effort es aceptable en esta categoría.** El rate limiting mitiga abuso (fuerza
   bruta sobre OAuth, saturación de escrituras), no garantiza un SLA de consumo; el
   deterioro bajo réplicas sin compartir es un límite más laxo, no una falla de seguridad.

**Cuándo y cómo se resolvería**

Si la app pasara a varias réplicas, se introduciría **Redis** una sola vez como infraestructura
compartida: contadores de ventana (las particiones de `AddRateLimiter` apuntan a Redis) y, al
mismo tiempo, backplane de SignalR (`AddStackExchangeRedis`). Las políticas pueden particionar
contra Redis sin cambiar el contrato HTTP (mismas cabeceras `RateLimit-*` y `429`), igual que
el backplane es un detalle de implementación para `ILogEventEmitter`. Hasta entonces, el estado
operativo entre instancias depende de una decisión única: SignalR y rate limiting escalan
**juntos**, con la misma pieza, o ninguno lo garantiza.

### HTTPS y HSTS: responsabilidad del proxy de borde, no de la API

El contenedor de la API **solo sirve HTTP** (`ASPNETCORE_URLS=http://+:3000`) y no publica
puerto: en producción solo nginx lo alcanza por la red interna. El TLS termina en el proxy
de borde que haya delante de nginx (balanceador, CDN o el propio nginx con certificado), y
**ese proxy es quien debe**:

- redirigir `http://` a `https://`,
- emitir `Strict-Transport-Security` (HSTS),
- reenviar `X-Forwarded-Proto` para que la API conozca el esquema original (ver
  `ReverseProxy` en la configuración).

La API **no** registra `UseHttpsRedirection`. No había ningún puerto HTTPS al que redirigir
(el middleware solo registraba el aviso `Failed to determine the https port for redirect`
en cada petición) y, además, un `307` sobre un `POST` de la API no aporta nada: el cliente
tiene que reenviar el cuerpo igualmente. La cookie de sesión sí se marca `Secure` en
`Production`, de modo que solo viaja por HTTPS aunque la API no lo imponga. Un test de
integración (`HttpsRedirectionTests`) fija que `GET /api/health` por HTTP responde `200`
sin `Location`, incluso con `HTTPS_PORT` configurado.

### El día del registro lo decide el cliente: el servidor solo lo acota a "UTC + 1 día"

Un registro pertenece a un **día calendario** (`logDate`, `yyyy-MM-dd`), y ese día es el que
el usuario vive en su zona horaria, no el de la hora UTC del servidor. Entre la medianoche
local y la UTC los dos días difieren: a las 20:00 en Bogotá (UTC-5) ya es el día siguiente
en UTC, y a las 08:00 en Tokio (UTC+9) todavía es el anterior. Si el cliente guardaba con su
fecha local y consultaba con la del servidor, el formulario "de hoy" abría vacío (o con el
registro de otro día) y el siguiente guardado sobrescribía el registro equivocado.

**La regla**

| Pieza                                   | Qué hace con el día                                                                 |
| --------------------------------------- | ----------------------------------------------------------------------------------- |
| Frontend (`utils/date.ts`)              | **Única fuente del "hoy"**: `todayIso()` calcula el día en hora local. Lo usan el guardado (`logDate`), la consulta (`GET /api/logs/today?date=`) y la ventana del panel (`from`/`to`). |
| `POST /api/logs`                        | Guarda el `logDate` que envía el cliente. El upsert es por `(user_id, log_date)`.   |
| `GET /api/logs/today?date=yyyy-MM-dd`   | Devuelve el registro de **ese** día. Sin `date`, usa el día UTC del servidor (comportamiento anterior, para no romper a otros clientes). |
| Validadores del servidor                | Solo **acotan** la fecha: no puede superar en más de 1 día la fecha UTC actual (`CreateDailyLogValidator` y `TodayQueryValidator`). |

**Por qué el límite es "UTC + 1 día"**

El servidor no conoce la zona del cliente y no debe fiarse de ella, así que no recalcula el
día: solo rechaza lo imposible. La zona más adelantada es UTC+14, donde ya es "mañana"
respecto al día UTC durante hasta 14 horas, de modo que ninguna fecha legítima supera la
fecha UTC + 1 día. Con `UTC + 1` se aceptan todos los clientes reales y se rechazan las
fechas claramente futuras (`UTC + 2` en adelante) con el mismo mensaje de antes,
`logDate cannot be in the future.`, y el sobre `VALIDATION_ERROR`. El pasado no se acota: se
puede registrar o corregir un día anterior.

**Qué se descarta y por qué**

- **Que el servidor decida el día con la zona del usuario.** Exigiría guardar y fiarse de una
  zona por usuario (o recibirla en cada petición), y el viaje entre zonas la haría incorrecta.
  El cliente ya sabe qué día vive.
- **Seguir con el día UTC en todo.** Es lo que causaba el defecto: en cualquier zona distinta
  de UTC hay horas del día en las que el "hoy" del usuario no coincide con el del servidor.

**Consecuencias**

- La fecha es un dato del cliente: un usuario puede registrar con un día que no es el suyo
  (dentro de la cota). No es un problema de seguridad: cada usuario solo escribe en sus propios
  registros y el campo describe su propio día.
- Un cliente que llame a `GET /api/logs/today` sin `date` obtiene el día UTC; el frontend
  siempre envía `date`.
- Los tests fijan la regla con relojes simulados: la utilidad de fechas se prueba en
  `Asia/Tokyo` y `America/Bogota`, el modal a las 20:00 de Bogotá (01:00 UTC del día
  siguiente) y un e2e con `timezoneId: 'America/Bogota'` que guarda, cierra y reabre el modal.

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

### Base de datos de pruebas: `docker-compose.test.yml`, sin Testcontainers (decisión de alcance)

Las pruebas de integración arrancan un PostgreSQL real de la misma versión que producción
(`postgres:16-alpine`) con `docker compose -f docker-compose.test.yml up -d`, y `make test`
lo levanta y lo baja alrededor de `dotnet test`. **No se usa Testcontainers para gestionar
ese contenedor desde el propio suite.** Es una decisión revisada con datos medidos en esta
máquina, no una omisión.

**Qué aporta hoy el compose para test**

| Aspecto                          | Estado actual                                                                                             |
| -------------------------------- | --------------------------------------------------------------------------------------------------------- |
| Aislamiento por ejecución        | Total: el compose monta `tmpfs` en `/var/lib/postgresql/data`, así que cada `up` parte de una base vacía |
| Versión de Postgres              | `16-alpine`, idéntica a `docker-compose.yml`                                                              |
| Coste de arranque                | `~0,2 s` si el contenedor ya está arriba                                                                  |
| Coste de un contenedor nuevo     | `~1,2 s` medido: `0,14 s` de `docker run` + `1,03 s` hasta que `pg_isready` responde                     |
| Ciclo completo de `make test`    | `~6,9 s` medido, de los cuales `~3 s` son las pruebas                                                    |
| Puerto                           | Fijo, `5433`, separado del `5432` del compose de desarrollo                                              |

**Motivos para no adoptar Testcontainers todavía**

1. **Docker ya es un prerrequisito, así que Testcontainers no elimina ninguno.** Los tests
   corren en el host y hoy levantan Postgres con compose. Testcontainers automatiza el
   *orquestador*, no el requisito: sin un demonio Docker, ninguna de las dos opciones
   funciona. La ventaja que más se le atribuye (un `dotnet test` autocontenido) no aporta
   aquí lo que aportaría en un proyecto sin orquestación previa.
2. **El aislamiento por ejecución ya está resuelto.** El `tmpfs` del compose garantiza una
   base vacía en cada `up`, que es exactamente lo que aportaría un contenedor efímero. Y
   desde `IntegrationTestBase` las pruebas ya truncan `users` y `daily_logs` entre casos,
   así que el estado no se hereda ni entre pruebas ni entre ejecuciones.
3. **Mantener ambas vías cuesta más de lo que ahorra.** Pedir compose como opción local
   rápida y Testcontainers como vía principal obliga a que la connection string deje de ser
   una constante en `CustomWebApplicationFactory` (hoy fija `localhost:5433`) y pase a
   inyectarse, con un valor por defecto para el compose y otro asignado dinámicamente por
   el puerto mapeado del contenedor. Eso son dos rutas de configuración para la misma base
   de datos y, por tanto, dos maneras de que los tests apunten a la instancia equivocada.
4. **El detonante todavía no ha llegado.** El beneficio mayor de Testcontainers es
   simplificar CI, y **este repositorio no tiene CI** (no existe `.github/workflows`). Se
   estaría pagando la complejidad de dos modos a cambio de una ventaja hipotética.

**Limitación que sí arrastra la decisión**: el puerto `5433` es fijo. Si otro proceso lo ocupa,
`make test` falla con un error de binding poco descriptivo en lugar de levantar la base en
otro puerto. Con un solo desarrollador en su máquina y el compose de desarrollo en `5432`, la
colisión es improbable.

**Cuándo y cómo se adoptaría**

El momento natural es que aparezca CI, y conviene hacerlo junto con consolidar los fixtures:
hoy las 5 clases de test usan `IClassFixture<CustomWebApplicationFactory>`, así que cada
una tiene su instancia y las migraciones de `Program.cs` se ejecutan 5 veces por ejecución.
Un contenedor a nivel de colección encaja con un `ICollectionFixture` único, que resolvería
las dos cosas de una vez: un solo arranque de Postgres, un solo `MigrateAsync` y un solo
`Program` en memoria.

Los cambios concretos serían: añadir `Testcontainers.PostgreSql` (4.15.0, compatible con
`net9.0`) al proyecto de tests; un `PostgreSqlBuilder` con `.WithImage("postgres:16-alpine")`
y las mismas credenciales del compose; su `GetConnectionString()` —que ya trae el puerto
mapeado— inyectado en la factory en vez de la constante; y el `IAsyncLifetime` del
contenedor conviviendo con el de `IntegrationTestBase`, que debe seguir truncando tablas y
no puede confundirse con el arranque del contenedor. La espera de disponibilidad la resuelve
la wait strategy de Testcontainers, que es más fiable que el reintento con backoff que hoy
ejecuta `DatabaseInitializer.WaitForDatabaseAsync`. El `docker-compose.test.yml` se
conservaría para el ciclo local, pero la connection string pasaría a ser siempre inyectada.
### Tipos del contrato: espejo a mano y OpenAPI como detector de deriva, no como fuente

`frontend/src/types/daily-log.ts` es el punto único donde el frontend describe el contrato
con la API, y hoy está escrito a mano. Existe `npm run generate:types`, que corre
`openapi-typescript` sobre el snapshot versionado `frontend/openapi.json` y produce
`frontend/src/types/api.generated.d.ts`. **El archivo generado se versiona pero no se
importa en la aplicación**: sirve para auditar que el espejo a mano no se desvía, no para
consumirlo. Es una decisión revisada contra el documento OpenAPI real de la API, no una
preferencia.

**Por qué el consumo directo del generado no es viable hoy**

1. **Falta un endpoint entero.** `GET /api/auth/me` documenta su `200` como
   `{ "description": "OK" }`, sin schema: `AuthController` devuelve un objeto anónimo que
   Swashbuckle no sabe describir. El tipo `User` es **inevitablemente manual**; un contrato
   "100 % generado" lo rompería en el primer import.
2. **El tipo generado rechaza un payload que la API sí acepta.** Swashbuckle traduce el
   `ActivityType?` de C# como `activityType?: ActivityType`, es decir opcional pero *no*
   nullable, así que envía `activityType: null` —que es como se dice "no hay actividad"— falla
   con `TS2322: Type 'null' is not assignable to type '"none" | "other" | ...'`. El espejo a
   mano declara `activityType: ActivityType | null`, que es lo correcto.
3. **`ApiErrorCode` se degrada a `string`.** El documento declara `ErrorBody.code` como
   `string` plano, así que se pierde el conjunto cerrado de los 5 códigos y con él la
   exhaustividad del `switch` y el autocompletado.
4. **Introduce 21 `| null` donde C# no admite null.** `createdAt: string | null` y
   `ErrorBody.message: string | null` son ruido defensivo en cada punto de uso, y además
   enmascaran la nullability que sí importa.
5. **Los enums dejan de ser iterables.** El generado emite uniones de literales
   (`"none" | "insomnia" | ...`) sin `as const`, así que la UI no puede recorrerlas para
   pintar selectores o sliders y necesita la lista a mano de nuevo, que es justo la
   duplicación que generar los tipos iba a eliminar. Los **valores sí coinciden**: 26 de 26
   verificados contra el `SnakeCaseEnumJsonConverter` real.
6. **El documento no contiene el conocimiento de dominio.** Los rangos que impone
   `CreateDailyLogValidator` (mood 1-5, ansiedad/estrés 1-10, sueño 0-24, actividad 0-600,
   366 días de rango), la regla de que `sleepDisturbances` y `symptoms` se envían siempre
   como `[]`, que `createdAt` viene en formato "O" de .NET y `new Date()` no lo parsea de
   forma fiable entre navegadores, y que `details[].field` es la única propiedad PascalCase
   del cable. Nada de eso está en el OpenAPI, y es la parte que más errores cuesta.
7. **La respuesta 429 no documenta cabeceras.** Los `RateLimit-*` que emite la API no están
   en el documento, así que el contrato generado tampoco los refleja.

**Por qué `openapi-typescript` no es una dependencia del proyecto**

`openapi-typescript@7.13.0` declara `typescript@^5.x` como peer y el proyecto usa
`typescript@6.0.3`, así que `npm install` falla con `ERESOLVE`. Se podría resolver con
`--legacy-peer-deps`, pero se comprobó que entonces **`npm ci` pasa a fallar sin ese flag**,
lo que rompe el `npm ci` del `Dockerfile` y cualquier CI futuro. El peer además es espurio:
`openapi-typescript` no importa `typescript` en su bundle, solo lo declara para el chequeo
opcional del `.d.ts` generado. Por eso el script invoca `npx --yes
openapi-typescript@7.13.0` con la versión pineada: el árbol de dependencias y el `npm ci`
se quedan intactos, a cambio de necesitar red la primera vez.

**Limitación que sí arrastra la decisión**

El snapshot `frontend/openapi.json` se queda desactualizado por sí solo: regenerarlo exige
arrancar la API en `Development` (que a su vez necesita Postgres, porque `Program.cs` ejecuta
las migraciones al inicio) y volver a volcar el documento. Sin CI que lo haga, el detector
de deriva solo corre cuando alguien se acuerda. Ese coste es aceptable mientras el
generado no se consuma, pero es la razón por la que esta decisión es revisable y no
definitiva.

**Cuándo y cómo se adoptaría el consumo directo**

Cuando el documento OpenAPI aprenda a describirse a sí mismo, es decir cuando se cierren
estos cuatro huecos: que `AuthController` deje de devolver un objeto anónimo y devuelva un
`UserResponse` documentado; que `ErrorBody.code` se declare como enum en el schema; que los
enums se emitan con `x-enumNames` o similar para poder recuperar la lista de valores; y que
las respuestas 429 documenten sus cabeceras. Con eso, el camino sería generar los tipos
base y mantener encima una capa fina a mano solo para lo que el documento no puede
expresar —los rangos de validación, la regla de `[]`, el formato de `createdAt`—, que es
justo la parte que hoy está en `daily-log.ts`. Hasta entonces, regenerar y mirar el diff es
la forma barata de detectar que un DTO del backend se movió.

## Decisiones de datos

La tabla `daily_logs` usa tipos relacionales para lo que tiene estructura (FK a `users`,
`date`, `timestamp with time zone`) y tres decisiones de almacenamiento deliberadas para el
resto: `symptoms` como `jsonb`, las escalas numéricas como `smallint` con el rango validado
en la aplicación, y los campos categóricos como `text` / `text[]` con el conjunto cerrado en
el modelo de la aplicación. No son los tipos que EF Core elegiría por defecto; son elecciones
de alcance que se justifican abajo.

### `symptoms` como `jsonb` y no una tabla hija normalizada

`symptoms` guarda una lista de `Symptom(Type, Severity)` serializada como JSON en una columna
`jsonb` del propio log. La alternativa relacional sería una tabla hija
`daily_log_symptoms(log_id, type, severity)` con FK a `daily_logs`.

**Motivos**

1. **Se lee y se escribe siempre completo, junto al log.** Tanto el `POST /api/logs` (que
   envía el registro entero) como el `GET /api/logs` (que lo devuelve entero) tratan los
   síntomas como parte indivisible del log. Nunca hay un caso de uso que actualice un síntoma
   aislado.
2. **Nunca se consulta por un síntoma suelto.** No existe una consulta del tipo "logs de
   usuarios con el síntoma X" ni un filtro por síntoma. Las tendencias y agregados iteran la
   lista en memoria una vez traído el rango de logs, no agregan por síntoma en la base. Sin
   esa consulta, la tabla hija no aporta nada.
3. **La tabla hija costaría más de lo que ahorra.** Añadiría un `join`, una segunda entidad
   con su configuración EF, reglas de cascada y un orden que hoy no existe, y obligaría a
   partir el `ON CONFLICT ... DO UPDATE` de una sola fila (`DailyLogRepository.UpsertAsync`)
   en varias sentencias dentro de una transacción.
4. **El formato queda flexible sin migración de esquema.** Agregar un miembro a `SymptomType`
   es un cambio de enum en C#, no una migración: el conjunto de valores válidos vive en el
   código, no en el esquema.

**Implicaciones técnicas**

- La columna es opaca para SQL: no se puede indexar ni filtrar por síntoma sin índices de
  expresión. Es aceptable porque no existe tal consulta.
- La serialización usa `JsonListConverter` con política `snake_case`, la misma del contrato
  HTTP; lo almacenado y lo que viaja por el cable coinciden.
- Se registra un `ValueComparer` (`ListComparer<T>`) para que EF Core detecte cambios en la
  lista y no considere que es la misma referencia.
- No hay validación de forma a nivel de esquema: el rango de `Severity` (1-5) lo valida
  `CreateDailyLogValidator` y los `SymptomType` fuera del conjunto cerrado los rechaza el
  `SnakeCaseEnumJsonConverter` al deserializar.

**Cuándo reconsiderar**: si apareciera una consulta real de "logs con el síntoma X" o un
agregado de "síntoma más frecuente" que deba ejecutarse en la base (sobre todo con un corpus
grande), una tabla hija con índice sobre `type` sería lo correcto.

### Escalas numéricas como `smallint` con el rango validado en la aplicación

Las escalas (`mood_rating`, `anxiety_level`, `stress_level`, `sleep_quality`) se almacenan
como `smallint`, `activity_minutes` como `smallint` nullable, y `sleep_hours` como
`numeric(3,1)`. Los rangos viven en `CreateDailyLogValidator` (mood 1-5, ansiedad/estrés 1-10,
calidad de sueño 1-5, minutos de actividad 0-600, horas de sueño 0-24), **no** en
restricciones `CHECK` del esquema.

**Motivos**

1. **Son un continuo, no un conjunto cerrado de etiquetas.** Estos valores se promedian y se
   grafican en tendencias; `smallint` es el entero más angosto que cubre el dominio
   (hasta 600) y evita la falsa precisión de `numeric` en un dato que es ordinal.
2. **El rango vive en un solo lugar.** Una restricción `CHECK` obligaría a duplicar la regla
   (validador + esquema) y a una migración cada vez que una escala cambie de rango. Con la
   validación en la aplicación, la única fuente de verdad es el validador.
3. **El error encaja con el contrato existente.** Validar en la aplicación produce el cuerpo
   estructurado `VALIDATION_ERROR` con el detalle por campo que el frontend ya consume
   (`details[].field`). Una `CHECK` fallida llegaría como `SQLSTATE 23514`, que el manejador
   global tendría que traducir a mano.
4. **`smallint` en lugar de `int`** es fiel al dominio (2 bytes frente a 4) y documenta que
   el valor es de rango pequeño; no implican una ganancia de espacio relevante, pero evitan
   un `int` que sugeriría un rango que no existe.

**Implicaciones técnicas**

- La base aceptará cualquier `smallint`; el único guardia es la aplicación. Quien escriba
  directo (psql, una migración, o un bug que salte el validador) puede dejar valores fuera de
  rango. Se acepta porque el repositorio es el único escritor y siempre pasa por la API.
- La regla queda verificada por pruebas de validación, no por el motor de base de datos. El
  contrato observable es el mismo: la API nunca persiste ni devuelve un valor fuera de rango
  por la vía normal.

**Cuándo reconsiderar**: si la integridad debe garantizarse con independencia de la
aplicación (varios escritores, ETL, o un almacén de datos que consuma la tabla), añadir
restricciones `CHECK` en el esquema.

### Campos categóricos como `text` / `text[]` con el conjunto cerrado en la aplicación, no `ENUM` de Postgres

`activity_type` y `social_frequency` son columnas `text`; `sleep_disturbances` es `text[]`.
Los valores son cadenas `snake_case` (`walking`, `frequent`, `insomnia`, …) que produce y
consume el enum de C# correspondiente (`ActivityType`, `SocialFrequency`, `SleepDisturbance`)
a través de `EnumSnakeCaseConverter` / `EnumListToStringArrayConverter`. El conjunto cerrado
de valores válidos **no** está en el esquema, sino en el modelo de la aplicación: el DTO
tipa el campo con el enum, y el `SnakeCaseEnumJsonConverter` rechaza cualquier cadena fuera
del conjunto al deserializar (400), antes de llegar al repositorio.

**Motivos**

1. **Agregar un valor es un cambio de código, no una migración.** Hoy sumar un miembro a
   `ActivityType` es una línea en el enum. Con un `ENUM` de Postgres sería un
   `ALTER TYPE ... ADD VALUE`, es decir una migración de esquema para un concepto de producto
   que cambia al ritmo de la app, no al de la base.
2. **`ENUM` de Postgres es costoso de evolucionar.** Añadir un valor tiene restricciones
   transaccionales y de bloqueo (históricamente no podía ejecutarse dentro de un bloque de
   transacción) y quitar o renombrar un valor obliga a recrear el tipo. El vocabulario de
   síntomas y actividades es justamente el que más se va a mover.
3. **Una sola fuente de verdad para el conjunto cerrado.** El valor persistido debe coincidir
   con el que viaja por el cable (`snake_case`). Con `text` + enum en código, la lista vive en
   el enum de C# y su converter; con un `ENUM` de Postgres, la lista la posee la base y el
   código pasa a reflejarla. Se prefiere que el contrato lo dicte el dominio.
4. **Sin acoplamiento ni orden innecesario.** Una columna `text` mapea limpio con Npgsql sin
   configuración de tipos; un `ENUM` de Postgres impone además un orden de comparación que
   nadie necesita, porque estos campos no se filtran ni se ordenan por valor.

**Implicaciones técnicas**

- La columna admite cualquier cadena a nivel de motor; el guardia es el enum tipado en el
  modelo. Se acepta por la misma razón que en las escalas: el repositorio es el único escritor.
- Renombrar un miembro del enum en código **sí** requiere tratar las cadenas `snake_case`
  persistidas como vocabulario apéndice: son la clave almacenada, así que un rename sin
  migración de datos deja huérfanas las filas antiguas.
- No hay integridad referencial ni validación de esquema para estos valores; tampoco índices,
  porque no se consultan.

**Cuándo reconsiderar**: si alguno de estos campos dejara de ser un vocabulario fijo y pasara
a ser una entidad de primera clase (por ejemplo, actividades definidas por el usuario), lo
correcto sería una tabla propia con FK, no un `ENUM` de Postgres.

## Flujo de autenticación con Google (OAuth 2.0 + OpenID Connect)

El login se resuelve con el flujo *authorization code* de OAuth 2.0, con `openid` en el scope
para obtener un `id_token` conforme a OpenID Connect. La lógica vive en
`Modules/Auth/AuthController` (inicio y callback), `GoogleOAuthClient` (intercambio y lectura
del `id_token`) y `UserRepository` (identidad del usuario). Esta sección describe el recorrido
paso a paso; el *por qué* de la cookie de sesión y de la ausencia de CSRF explícito está en
"Sesión: JWT en cookie `httpOnly`" y "CSRF y SameSite=Lax", arriba.

### Recorrido paso a paso

| Paso | Quién actúa | Qué ocurre |
| ---- | ----------- | ---------- |
| 1 | Navegador | El usuario pulsa "Continue with Google", un `<a href="/api/auth/google">` (navegación de página entera, no `fetch`, para que el backend pueda fijar cookies). |
| 2 | Frontend → Backend | `GET /api/auth/google` (`AuthController.GoogleLogin`). |
| 3 | Backend | Genera un `state` aleatorio y fija la cookie `oauth_state`; responde `302` al endpoint de autorización de Google con ese `state`. |
| 4 | Google | Muestra la pantalla de consentimiento y redirige al `redirect_uri` con `code` y `state`. |
| 5 | Google → Backend | `GET /api/auth/google/callback?code=…&state=…` (`AuthController.GoogleCallback`). |
| 6 | Backend | Borra la cookie `oauth_state` y valida `state` contra ella. Si no cuadra, redirige a `/login?error=auth_failed` (login CSRF). |
| 7 | Backend → Google | Intercambio **servidor-a-servidor** del `code` por tokens en el endpoint de token. |
| 8 | Backend | Valida el `id_token` recibido (firma, expiración y audiencia). |
| 9 | Backend | Upsert del usuario por `sub` (`UserRepository.UpsertByGoogleIdAsync`). |
| 10 | Backend | Firma el JWT y lo envía en la cookie de sesión `access_token`; responde `302` a `${FrontendUrl}/dashboard`. |

Cualquier excepción entre el paso 6 y el 10 se registra y se traduce en el mismo redirect a
`/login?error=auth_failed`: al navegador nunca le llega el detalle interno (mensajes de Google,
excepciones de validación) ni un `500` en el cuerpo.

### Paso 2–3: inicio del flujo y cookie `state`

`GoogleLogin` no confía en ningún dato del navegador: **inventa** el `state` con
`RandomNumberGenerator.Fill` sobre 32 bytes codificados con `WebEncoders.Base64UrlEncode` (no
`Random`, que no es apto para secretos). Después:

- Fija la cookie `oauth_state` con las mismas opciones centralizadas (`HttpOnly`,
  `SameSite=Lax`, `Path=/`, `Secure` solo en `Production`) más `Max-Age=10 min`: el `state` no
  es una credencial de sesión, así que caduca rápido y no se espera que sobreviva a la visita.
- Redirige a `GoogleOAuthClient.BuildAuthUrl(state)`, que arma la URL de autorización
  (`accounts.google.com/o/oauth2/v2/auth`) con `response_type=code`, `scope=openid email profile`,
  `access_type=online` y `prompt=select_account`.

El `state` viaja por dos canales: en la query hacia Google y en la cookie `httpOnly`. Solo el
segundo lo controla el servidor; el primero pasa por el navegador de terceros.

### Paso 6: consumo de la cookie y anti-CSRF

El callback borra `oauth_state` **antes** de validar (`Response.Cookies.Delete`): la cookie es
de un solo uso, así que un `state` filtrado no puede reutilizarse. La comparación
(`StateMatches`) exige que exista la cookie y que `state` tenga la misma longitud, y luego usa
`CryptographicOperations.FixedTimeEquals` para no filtrar por *timing* cuántos caracteres
coinciden. Si algo falla se redirige a `/login?error=auth_failed`. Esto es lo que neutraliza
el *login CSRF* (que un atacante inserte su propio `code` en la sesión de la víctima), ya que
Google no envía la cookie `oauth_state`: sin la cookie que fijó el propio backend, no hay
forma de que el `state` de vuelta coincida.

### Paso 7: intercambio servidor-a-servidor del `code`

`GoogleOAuthClient.ExchangeCodeAsync` hace un `POST` a
`https://oauth2.googleapis.com/token` (`application/x-www-form-urlencoded`) con:

| Parámetro       | Origen              |
| --------------- | ------------------- |
| `code`          | query del callback  |
| `client_id`     | `GoogleOAuth:ClientId` |
| `client_secret` | `GoogleOAuth:ClientSecret` |
| `redirect_uri`  | `GoogleOAuth:RedirectUri` |
| `grant_type`    | literal `authorization_code` |

El `redirect_uri` debe ser la ruta real del callback, `GET /api/auth/google/callback`, a
través del origen que sirve el frontend (Vite en desarrollo, nginx en producción): así la
cookie `oauth_state` se escribe y se lee en el mismo origen. Esa URI exacta debe estar
registrada en *Authorized redirect URIs* de la consola de Google Cloud; Google compara el
valor carácter a carácter y, si no coincide, responde `redirect_uri_mismatch`.

Dos propiedades son importantes: el **`client_secret` no abandona el backend** (solo aparece
en esta llamada saliente), y el **`code` se vuelve a pasar tal cual**; su validez la determina
Google, no el backend. Un fallo de la llamada (`EnsureSuccessStatusCode`) o una respuesta sin
`id_token` lanzan y terminan en el redirect de error.

### Paso 8: validación del `id_token`

El token endpoint devuelve un `id_token` (JWT firmado por Google). El backend lo valida con
`GoogleJsonWebSignature.ValidateAsync` y `ValidationSettings` con **`Audience = [ClientId]`**:
eso comprueba la firma contra las claves públicas de Google, la expiración y que el token
estuviera dirigido a *esta* aplicación (si no, un `id_token` emitido para otra app serviría para
suplantar). Tras validar, exige un `sub` no vacío (`UnauthorizedException` si falta) y mapea el
payload a `GoogleProfile(Subject, Email, Name, Picture)`. El `sub` es el identificador estable
del usuario en Google; el `email` y el `name` son atributos mutables del perfil.

### Paso 9: upsert por `sub`

`UserRepository.UpsertByGoogleIdAsync` busca al usuario **por `GoogleId` (= `sub`)** y, si no
existe, lo crea; en cualquier caso sobrescribe `Email`, `Name`, `AvatarUrl` y `UpdatedAt`. Se
elige `sub` y no `email` como clave de identidad porque el email de una cuenta de Google puede
cambiar (el usuario lo edita en su cuenta) mientras que el `sub` es inmutable. `google_id` y
`email` tienen índices únicos en `users`; el upsert es idempotente sobre `google_id`, así que
un segundo login del mismo usuario devuelve la misma fila en vez de duplicarla.

### Paso 10: emisión de la sesión

Con el `Id` del usuario, `JwtService.Sign` firma un JWT `HS256` (claims `sub` = `userId`, `email`,
`iat`, `exp` a 7 días) y `GoogleCallback` lo envía en la cookie `access_token` (`HttpOnly`,
`SameSite=Lax`, `Max-Age` de 7 días) antes de redirigir a `/dashboard`. A partir de ahí la
cookie es la única credencial: la usan por igual la API HTTP (vía `RequireAuthMiddleware`) y el
hub SignalR (ver "Canal de tiempo real"), y `POST /api/auth/logout` la borra en el cliente sin
estado en el servidor.

### Notas de borde

- **Rate limiting**: el callback es el único endpoint sin sesión que provoca una llamada
  externa y un upsert, así que está limitado a 20 req/15 min por IP (política `auth`). El
  endpoint de inicio `GET /api/auth/google` no hace trabajo externo ni de base de datos, así
  que no se limita.
- **Modo demo/e2e**: en el entorno `Testing` se registra `POST /api/auth/test-login`, que
  replica el tramo final del flujo (mismo upsert por `sub` derivado del email y misma cookie de
  sesión) para que los e2e entren autenticados sin pasar por Google.

## Observabilidad

El backend no tiene un stack de métricas/tracing distribuido: se observa con **logs
estructurados de Serilog** y una **correlación por request**. Esta sección cubre las dos piezas
que hacen esos logs útiles y seguros (el identificador de correlación y la redacción de
credenciales) y la decisión de salud: por qué `/api/health` prueba la base de verdad.

### Correlación por `X-Request-Id`

`RequestIdMiddleware` (`RequestIdMiddleware.cs`) corre muy temprano en el pipeline, justo
después del manejador global de excepciones y antes de seguridad, CORS, rate limiting y
autenticación (`app.UseRequestId()` en `Program.cs`). Por cada petición:

1. Lee la cabecera entrante `X-Request-Id`; si falta o viene vacía, genera una nueva con
   `Guid.NewGuid().ToString("N")`.
2. Escribe ese mismo valor en la cabecera de respuesta `X-Request-Id`.
3. Empuja `RequestId` a `LogContext` de Serilog dentro de un `using`, de modo que quede
   enriquecido en **todos** los eventos emitidos durante la petición y se limpie al terminar,
   sin filtrarse a la siguiente.

**Motivos**

1. **Un identificador cose el rastro de una petición.** El cliente recibe en la respuesta el
   mismo id que aparece en cada línea de log del backend, así que un reporte de error se puede
   reducir a filtrar por `RequestId` en los logs.
2. **El `RequestId` va en el output, no en el mensaje.** En Development el template de consola
   incluye `[{RequestId}]`; en producción el `CompactJsonFormatter` lo emite como propiedad
   estructurada (ver `SerilogConfigurationExtensions`), de modo que es consultable sin parsear
   texto.
3. **Aceptar el id del cliente es deliberado.** Permite que un proxy o el frontend propaguen su
   propia correlación en lugar de que el backend imponga una. No se valida su formato ni se usa
   para nada más que correlación: no es un secreto ni un dato de autorización.

**Implicaciones técnicas**

- La cabecera de respuesta se fija antes de invocar el resto del pipeline, así que acompaña
  también a las respuestas de error (401, 429, 4xx/5xx) generadas más abajo.
- No hay propagación saliente: el único llamado a un tercero es el intercambio OAuth con
  Google, que no reenvía el `X-Request-Id`. Si se añadieran llamadas entre servicios, habría
  que propagar la cabecera explícitamente para no cortar la traza.

### Redacción de cookies y cabeceras de autorización

`RedactSensitiveHeadersPolicy` (`Core/Configuration`) es un `IDestructuringPolicy` de Serilog
registrado de forma global (`Destructure.With(...)` en `SerilogConfigurationExtensions`). Cuando
un evento destructure un diccionario de cabeceras —`IDictionary<string, StringValues>`, que es
exactamente el tipo de `HttpContext.Request.Headers` y `Response.Headers`— la política lo
reconstruye con un `StructureValue` donde `Cookie`, `Set-Cookie` y `Authorization` se sustituyen
por `[REDACTED]`; el resto de cabeceras viaja tal cual. La comparación de nombres es
*case-insensitive*, así que `cookie` y `Cookie` se tratan igual.

**Motivos**

1. **Esas tres cabeceras son credenciales.** La cookie `access_token` es la sesión entera y
   `Authorization` puede llevar un bearer token; registrarlos en claro deja un secreto
   persistido en los logs, que suelen ser el artefacto más copiado, agregado y compartido del
   sistema.
2. **La redacción vive en la frontera de logging, no en cada llamada.** Al aplicarse como
   política de Serilog, cualquier `logger.Information("... {@Headers}", headers)` queda
   redactado sin que el sitio de llamada tenga que acordarse. Es una garantía por defecto, no
   una convención.
3. **El test lo fija como contrato.** `SensitiveHeadersTests` afirma que incluso a nivel
   `Debug` los valores de `Cookie` y `Authorization` nunca aparecen en la salida y que sí
   aparece `[REDACTED]`.

**Limitación**: la redacción depende de que el valor se destructure como ese diccionario. Un
sitio que ya haya formateado las cabeceras a string (p. ej. `headers.ToString()`) y lo registre
como texto plano no pasa por la política; por eso la convención es registrar el diccionario con
`{@Headers}`, nunca su representación textual.

### `GET /api/health` consulta la base (`SELECT 1`), no devuelve un literal

El endpoint de salud (`Program.cs`) abre una conexión real con `NpgsqlConnection`, ejecuta
`SELECT 1` y responde `200 {"status":"ok","uptime":…}` solo si la consulta tiene éxito. Si falla,
registra la excepción y responde `503 {"status":"error","db":"unreachable"}`. No está decorado
con `[RequireAuth]`: los orquestadores lo consultan sin sesión.

**Motivos**

1. **Un `200` constante enmascara caídas de la base.** El healthcheck no es para el usuario sino
   para el orquestador (el `healthcheck` de Docker Compose, `curl`/`wget` contra `/api/health`
   en desarrollo y producción): si siempre devolviera `ok`, un contenedor con la base caída
   seguiría marcado como *healthy*, no se reiniciaría, no habría alerta y los clientes fallarían
   más tarde con errores confusos. Solo una consulta real refleja la disponibilidad real.
2. **La disponibilidad que importa es la del sistema completo, no la del proceso.** La API no
   sirve para nada sin Postgres; llamar sano a un proceso que no puede leer ni escribir datos
   sería una definición falsa de salud.
3. **`uptime` es informativo, no la señal.** Se incluye para diagnóstico humano; la señal
   automática es el código de estado.

**Implicaciones técnicas**

- El endpoint hereda la dependencia del pool de Postgres: una base lenta lo hace lento. No hay
  timeout propio más allá de los defaults de Npgsql.
- No filtra detalle de infraestructura al cliente: la excepción va al log y la respuesta es un
  literal genérico.

**Cuándo reconsiderar**: si se necesitara separar *liveness* de *readiness* (un reinicio del
proceso no debería dispararse por una base momentáneamente caída), lo correcto es dividir en
`/live` —el proceso responde— y `/ready` —la base responde—, y apuntar cada sonda al endpoint
que le corresponde.

## Límites conocidos de alcance

Tres límites del sistema son **decisiones conscientes de alcance**, no deuda accidental: se
aceptan hoy porque el despliegue es de **una sola instancia** y cada uno tiene ya identificada
su mitigación. Se listan juntos para que quede claro que comparten la misma causa y que se
revocan con dos piezas de infraestructura (Redis y un job de migración), no con cambios de
diseño de la aplicación.

| Límite | Qué pasa hoy | Cuándo se rompe | Mitigación identificada |
| ------ | ------------ | --------------- | ----------------------- |
| SignalR sin backplane | El estado de grupos (`user:{userId}`) y conexiones vive en la memoria de cada instancia | Varias réplicas: el evento de una escritura procesada por la instancia B no llega a un usuario conectado a la A | Backplane de SignalR (Redis o Azure SignalR Service), registrado en `AddSignalR()` |
| Rate limiting en memoria | Los contadores de `FixedWindowRateLimiter` viven en la memoria de la instancia | Varias réplicas: cada una cuenta por separado y el límite deja de ser una garantía global | Contadores distribuidos en Redis (`INCR`/`EXPIRE`), la misma pieza que el backplane de SignalR |
| Migraciones al arrancar | La API aplica las migraciones con `DatabaseInitializer.MigrateAsync` antes de empezar a escuchar | Varias réplicas: varias instancias compiten por migrar el esquema a la vez | Job de migración separado, previo a escalar la API |

### SignalR sin backplane

Detalle completo en "Escalado horizontal del canal en tiempo real: señalada limitación, sin
backplane (decisión de alcance)". En una sola réplica los grupos y el broadcast por conexión
funcionan porque todo vive en la misma memoria; el estado real nunca se pierde, porque la falla
es solo de *entrega inmediata* del evento y un refresco HTTP devuelve el dato persistido. El
emisor (`ILogEventEmitter`) no conoce el transporte, así que introducir el backplane es un
detalle de infraestructura que no toca el dominio.

### Rate limiting en memoria

Detalle completo en "Rate limiting en memoria: mismo backplane pendiente que SignalR (decisión
de alcance)". Con una sola réplica el contador es local y exacto; con varias, el límite pasa de
ser una garantía global a ser probabilístico. Es best-effort *por diseño*: mitiga abuso, no
garantiza un SLA de consumo. Se trata como **la misma decisión** que el backplane de SignalR
porque ambas se resuelven con la misma dependencia operativa: **Redis**. Se introducen juntas o
no se introducen.

### Migraciones aplicadas al arrancar

`Program.cs` llama a `DatabaseInitializer.MigrateAsync` durante el arranque, antes de exponer
tráfico, y **falla en firme** si no puede migrar; así la API nunca sirve peticiones contra un
esquema desactualizado. Antes de migrar espera a que la base acepte conexiones con reintentos y
backoff exponencial (`WaitForDatabaseAsync`: hasta 10 intentos, de 1 s a 10 s), para tolerar que
el contenedor de la base esté reiniciándose cuando arranca la API.

**Por qué se acepta hoy**: con una única instancia no hay competencia por el esquema, y migrar
en el arranque es la forma más simple de garantizar "esquema al día antes de servir". También es
lo que hace que el entorno de test sea autocontenido: `Program.cs` aplica las migraciones al
iniciar, así que las pruebas de integración no las ejecutan por su cuenta.

**Qué se rompe con varias réplicas**: cada instancia intentaría migrar el mismo esquema a la
vez; EF Core usa bloqueos para serializar, pero se producen esperas, contención y, ante un fallo
parcial, riesgo de quedar en un estado intermedio con varias instancias reintentando. El arranque
de la aplicación queda además acoplado a la disponibilidad de la base.

**Mitigación identificada**: sacar las migraciones de la API y correrlas como un **job de
migración separado** antes de desplegar/escalar (un `docker compose run` o un job de
Kubernetes/CI que termina antes de que el servicio reciba tráfico). La API arrancaría sin migrar
y el despliegue garantizaría el esquema como paso previo. Es la evolución natural junto con
Redis: ambos separan el estado operativo (esquema, grupos/contadores) del proceso de la API.

## De Node/React a .NET/Vue

Este proyecto es un cambio deliberado de stack: el mismo dominio (autenticación, registro
diario, tiempo real) resuelto con el ecosistema .NET/Vue en lugar de Node/React. La tabla mapea
concepto a concepto; después se detallan los tres cambios que **no** son una traducción literal
sino un cambio de diseño: la inyección de dependencias del framework, la validación con
FluentValidation en lugar de zod y los grupos de SignalR en lugar de las *rooms* de Socket.IO.

### Tabla de equivalencias

| Concepto | Node/React | .NET/Vue en este proyecto |
| -------- | ---------- | ------------------------- |
| Runtime | Node.js | .NET 9 |
| Lenguaje | TypeScript | C# 13 (`Nullable` + `TreatWarningsAsErrors`) |
| Framework HTTP | Fastify | ASP.NET Core (controllers + middleware) |
| Composición / DI | plugins de Fastify o contenedor manual (tsyringe, awilix) | contenedor integrado, composition root en `Program.cs` |
| Validación | zod (`.parse` dentro del handler) | FluentValidation + filtro de acción global |
| Acceso a datos | TypeORM (entidades + repositorios) | EF Core + Npgsql (`IEntityTypeConfiguration` + repositorios) |
| Base de datos | PostgreSQL | PostgreSQL (igual) |
| Migraciones | scripts / migraciones de TypeORM | migraciones de EF Core aplicadas al arrancar |
| Sesión y auth | `jsonwebtoken` + cookies | JWT HS256 propio + cookie `httpOnly` + `RequireAuthMiddleware` |
| Tiempo real (servidor) | Socket.IO con *rooms* | SignalR con *groups* (+ `IHubContext`) |
| Tiempo real (cliente) | `socket.io-client` | `@microsoft/signalr` |
| Estado del front | Redux / Zustand / Context | Pinia |
| Routing del front | React Router | Vue Router (con guard global) |
| Cliente HTTP | `fetch` / axios | `fetch` envuelto en `api-client.ts` |
| Estilos | Tailwind CSS | Tailwind CSS (igual) |
| Build | Vite | Vite (igual) |
| Tests backend | Jest + Supertest | xUnit + `WebApplicationFactory` + coverlet |
| Tests frontend | Jest/Vitest + React Testing Library | Vitest + `@vue/test-utils` + jsdom |
| Logging | pino / winston | Serilog (con redacción de credenciales) |
| Documentación de API | swagger-jsdoc / zod-openapi | Swashbuckle (OpenAPI derivado de los controllers) |
| Config y secretos | `dotenv` | `appsettings` + variables de entorno + `SecretFileConfiguration` |

### Inyección de dependencias del framework

**En Node** la composición suele ser explícita a mano (importar y pasar instancias) o delegada a
un contenedor opcional (`tsyringe`, `awilix`) o a la encapsulación de plugins de Fastify; las
dependencias se resuelven donde se declaran. **En .NET** el contenedor viene con el framework:
todo el grafo se declara de forma centralizada en `Program.cs` con su *lifetime*
(`AddScoped<IUserRepository, UserRepository>`, `AddSingleton<JwtService>`,
`AddHttpClient<GoogleOAuthClient>()` como cliente tipado) y las clases reciben lo que necesitan
por **constructor injection**.

Lo que cambió en el diseño, más allá de la sintaxis:

1. **La composición es un artefacto de primera clase.** El "wiring" no está repartido por los
   módulos: hay un único lugar donde se ve qué implementa cada interfaz y con qué ciclo de vida.
   Eso hace explícita la separación controller → service → repository, que se sostiene porque
   cada capa recibe sus colaboradores inyectados y no los construye.
2. **El ciclo de vida es parte del contrato.** `Scoped` ata una instancia al request (el
   `AppDbContext`, los repositorios); `Singleton` guarda lo que no tiene estado por petición
   (`JwtService`, `AuthCookieOptions`). Es una decisión que en Node normalmente se resuelve a
   mano o no se explicita.
3. **La validación de configuración arranca con la app.** `AddOptions<T>().ValidateOnStart()`
   aborta el proceso si falta un secreto (JWT, Google OAuth, connection string), en vez de
   fallar en la primera petición que lo use.
4. **El grafo entero es sustituible en los tests.** `WebApplicationFactory` levanta el mismo
   `Program` con configuración de test; el resto de los detalles (mirar
   "Separación de capas") se apoya en esta composición.

### FluentValidation en lugar de zod

Con **zod** el patrón típico es declarar el esquema y llamar a `schema.parse(req.body)` dentro de
cada handler. Aquí la validación es una **preocupación transversal**: `CreateDailyLogValidator`
(una clase con reglas tipadas) se resuelve por DI y la ejecuta `FluentValidationActionFilter`
como filtro global **antes** de que el controller se ejecute.

Lo que cambió:

1. **El controller no valida.** No hay una llamada a `parse` dentro del handler; el filtro corre
   antes y, si algo falla, lanza `ValidationException` (error de dominio), que
   `GlobalExceptionHandler` traduce al sobre `{ error: { code: "VALIDATION_ERROR", details } }`
   con un detalle por campo. La misma forma de error que el frontend ya consume.
2. **Las reglas son objetos de primera clase, no esquemas sueltos.** Al ser clases con DI, un
   validador puede depender de otros servicios y reutilizarse; y el rango (mood 1-5, ansiedad
   1-10, etc.) vive en un único sitio.
3. **Las mismas reglas alimentan la documentación.** `FluentValidationSchemaFilter` traduce las
   reglas a `minimum`/`maximum` en el OpenAPI. Con zod el equivalente exige un puente
   (zod-openapi) que mantenga esquema y documento sincronizados. Detalle en "Tipos del
   contrato".

### Grupos de SignalR en lugar de rooms de Socket.IO

En **Socket.IO** el servidor conserva la referencia del socket y hace `socket.join('user:'+id)`;
emitir a la *room* es una llamada sobre una conexión concreta. En **SignalR** la idea es
parecida (`Groups.AddToGroupAsync(connectionId, "user:{userId}")` en `OnConnectedAsync`), pero el
emisor **no** necesita conocer el hub: `LogEventEmitter` usa `IHubContext<LogsHub>` para enviar a
`Clients.Group(...)`.

Lo que cambió:

1. **El emisor está desacoplado del transporte.** El servicio que persiste el log emite a través
   de la interfaz `ILogEventEmitter`; ni sabe que debajo hay SignalR ni maneja conexiones. Eso es
   lo que permite tratar el backplane como un detalle de infraestructura (ver "Límites conocidos
   de alcance").
2. **La conexión se autentica con la misma sesión.** El hub se marca con `[RequireAuth]` y se
   apoya en la cookie `httpOnly` que ya usa la API; no hay un segundo token de handshake. En el
   cliente, `@microsoft/signalr` se construye con `withCredentials` y `withAutomaticReconnect`.
3. **Los grupos se derivan de la identidad, no del socket.** El nombre `user:{userId}` sale del
   claim del usuario autenticado, no de un identificador de conexión, así que reconectar
   reingresa al mismo grupo.

### Lo que no cambió

El stack nuevo no reescribió los límites del dominio: se mantiene el patrón repositorio con
upsert atómico (`ON CONFLICT`), la sesión en cookie `httpOnly` con la misma lógica de
`SameSite=Lax` y CSRF, el tipado fuerte del contrato, PostgreSQL como base y Vite + Tailwind en
el front. El stack es lo que cambió; las decisiones de diseño documentadas arriba son, en buena
parte, independientes de él.
