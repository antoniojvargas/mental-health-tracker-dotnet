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

- Cookie JWT con atributos `HttpOnly`, `Secure`, `SameSite=Lax` y `SameSite` coherente
  con el flujo de redirect de OAuth (`Lax` permite la cookie en navegaciones de
  scripting-safe top-level manteniendo el token inaccesible a JS).
- El frontend no administra el token: la sesión queda asociada a la cookie y el
  backend autentica cada petición a partir de ella.
- El cierre de sesión se realiza invalidando/borrando la cookie desde el backend.