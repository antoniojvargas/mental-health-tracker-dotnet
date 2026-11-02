# ADR 0003: Confianza en el proxy: la API no se expone, se alcanza a través de nginx

- **Estado:** Aceptada
- **Fecha:** 2026-10-12
- **Áreas:** backend, infraestructura

## Contexto

La API no está pensada para recibir tráfico de Internet. Kestrel solo habla HTTP (`:3000`) y, en
producción, **no publica puerto**: el único punto de entrada es nginx (el contenedor del frontend,
`:8080`), que sirve el bundle y proxifica `/api/` y `/hub` por la red interna de Compose. Toda
decisión que depende de «quién es el cliente» se apoya en esa cadena y en saber de quién fiarse:

```
cliente ──► [proxy de borde con TLS, opcional] ──► nginx :8080 ──► Kestrel :3000 (red 172.28.0.0/24)
```

## Decisión

Se confía en el proxy de forma explícita y acotada, y solo en producción:

| Reglas del modelo | Dónde se aplican |
| --- | --- |
| **Solo se fía de proxies declarados.** `X-Forwarded-For` y `X-Forwarded-Proto` solo se aceptan si la petición llega de una IP de `ReverseProxy:KnownProxies` o de una red de `ReverseProxy:KnownNetworks` (en producción, la subred fija `172.28.0.0/24`). De cualquier otro origen se ignoran y se conserva la IP real del socket. | `ReverseProxyServiceCollectionExtensions` |
| **Fiarse es explícito.** `UseForwardedHeaders` solo se registra con `ReverseProxy:Enabled=true`, es el **primer** middleware (antes del log de peticiones y del rate limiter), y `Enabled=true` sin proxies ni redes impide arrancar. | `ApiPipeline`, `ReverseProxyOptionsValidator` |
| **`ForwardLimit=1`: se lee solo la entrada más a la derecha**, la que añade nuestro nginx con `$proxy_add_x_forwarded_for`. Lo que el cliente escriba a la izquierda no cuenta. | `ReverseProxyOptions.ForwardLimit` (detalle en «Rate limiting en memoria») |
| **El puerto de la API no se publica.** Si se publicara, cualquiera podría saltarse nginx; la protección de arriba se apoya en que desde fuera solo se llega por nginx. | `docker-compose.prod.yml` |

## Alternativas descartadas

Las salidas obvias que no se tomaron:

- **Leer `RemoteIpAddress` tal cual** (suponer que Kestrel recibe al cliente): detrás de nginx todos
  los usuarios compartirían la IP del proxy y, con ella, la cuota de la política `auth`.
- **Confiar en cualquier `X-Forwarded-For`**: cualquier cliente podría inventarse la IP y repartir sus
  intentos entre direcciones falsas. Por eso `Enabled=true` sin proxies ni redes declarados no arranca.
- **Leer toda la cadena de `X-Forwarded-For`**: lo que el cliente escribe a la izquierda no es de fiar;
  con `ForwardLimit=1` solo cuenta la entrada que añade nuestro nginx.

## Consecuencias

### Qué depende de ello

| Consumidor | Qué necesita | Consecuencia si falla la confianza |
| --- | --- | --- |
| Política de rate limiting `auth` (y su clave en Redis) | La IP real del cliente en `RemoteIpAddress` | Sin proxy de confianza todos comparten la IP de nginx (un atacante bloquearía el login de todos); con confianza de más, un cliente se inventaría IPs y esquivaría la cuota |
| `ClientIp` del log de peticiones | La misma IP ya resuelta | Logs con la IP del proxy, inútiles para investigar abuso |
| Esquema de la petición (`X-Forwarded-Proto`) | Saber si el original fue HTTPS | La API no impone HTTPS (ver la sección siguiente); nginx fija esta cabecera con su propio `$scheme`, que en `:8080` es `http`, así que con un proxy de TLS delante habría que propagar la cabecera del proxy de borde en la configuración de nginx |

### Desarrollo y tests

`ReverseProxy` viene desactivado, así que `RemoteIpAddress` es lo que ve el
socket. Llamando a `:3000` es el cliente; pasando por Vite (`:5173`, que no añade `X-Forwarded-For`)
es la IP del proxy de desarrollo. Con un solo usuario no importa, y los tests lo simulan con un
`IStartupFilter` que fija la IP de origen (`ForwardedHeadersTests`).

### Qué rompe la suposición (y obliga a revisar esta decisión)

publicar el puerto de la API; poner otro proxy delante de nginx sin subir
`ForwardLimit` a 2, añadir su red a `KnownNetworks` y configurar nginx para confiar en él (`real_ip`);
o cambiar la subred de Compose sin actualizarla en `ReverseProxy__KnownNetworks__0`.

## Referencias

- `ApiPipeline`, `ReverseProxyOptions`, `ReverseProxyOptionsValidator` y `ReverseProxyServiceCollectionExtensions` (backend); `frontend/nginx.conf` y `docker-compose.prod.yml`.
- `ForwardedHeadersTests` (integración): la cuota `auth` se particiona por la IP reenviada, el `X-Forwarded-For` de una dirección no declarada se ignora y el esquema reenviado solo vale desde un proxy de confianza. `ReverseProxyOptionsTests` (unitarias): las opciones y su validación. `DistributedRateLimitingTests`: con Redis, la clave `auth` usa la IP real.
- [`architecture.md`](../architecture.md#modelo-de-confianza-en-el-proxy-la-api-no-se-expone-se-alcanza-a-través-de-nginx) y, para el TLS, «HTTPS y HSTS» en el mismo documento.
