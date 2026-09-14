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