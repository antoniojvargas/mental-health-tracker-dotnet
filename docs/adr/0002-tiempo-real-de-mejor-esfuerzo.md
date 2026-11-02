# ADR 0002: El tiempo real es de mejor esfuerzo; la fuente de verdad es la API

- **Estado:** Aceptada
- **Fecha:** 2026-10-13
- **Áreas:** backend, frontend

## Contexto

El evento `log:created` / `log:updated` es un **aviso de comodidad**, no un canal fiable de
datos.

Hay que decidir qué pasa cuando el aviso falla en el servidor o no llega al cliente, y qué puede
dar por supuesto el resto de la aplicación.

## Decisión

Dos reglas lo acotan:

- **En el servidor, un fallo del hub no rompe el guardado.** `LogEventEmitter` se ejecuta
  cuando el registro ya está confirmado en la base de datos. Si `SendAsync` falla, captura la
  excepción, la registra en `Warning` (con el nombre del evento y el `userId`) y **no la
  relanza**: `POST /api/logs` responde igualmente 201. Solo deja pasar
  `OperationCanceledException`. Además envía con `CancellationToken.None`, de modo que si el
  cliente que guardó corta la conexión tras el commit, sus otras pestañas se enteran igual.
- **En el cliente, lo que se pierde se recupera pidiéndolo a la API.** SignalR no reenvía los
  eventos emitidos mientras una conexión estaba caída. Por eso `useLogSocket` acepta un
  callback `onReconnected`, que se dispara con `onreconnected` de la reconexión automática, y
  el dashboard lo usa para volver a pedir la ventana activa (`logsStore.fetch`). Mientras
  tanto, los eventos que sí llegan se integran con `mergeLog`, que reemplaza por `logDate` y
  por tanto es idempotente.

## Alternativas descartadas

Las salidas obvias que no se tomaron:

- **Hacer que el guardado dependa del envío del evento** (fallar o reintentar si el hub falla):
  convertiría una comodidad en un punto de fallo del guardado, que ya está confirmado en la base.
- **Un canal con entrega garantizada** (reenvío de lo perdido en la reconexión): más estado en el
  servidor y en el cliente para evitar una lectura a la API que ya existe y es barata.

## Consecuencias

Consecuencia de diseño: **nada puede depender de que un evento llegue**. Toda pantalla debe
poder reconstruirse con una lectura a la API; el hub solo evita tener que recargar.

- Con varias réplicas, el aviso solo llega a clientes conectados a otra instancia si hay backplane
  (ver «Redis opcional» en [`architecture.md`](../architecture.md)); sin él, la regla de arriba es
  justo lo que mantiene correcto el sistema.
- Los tests fijan la regla: `LogEventEmitterTests` (grupo `user:{id}`, nombre del evento, payload y que
  un fallo del hub se registra y no se propaga) y los de `useLogSocket` (reconexión y rechazo de
  `start()` registrado con `console.warn`).

## Referencias

- `LogEventEmitter`, `LogHubEvents` y `HubRoutes` (backend); `hub-client.ts`, `hub-events.ts` y `useLogSocket.ts` (frontend).
- [`architecture.md`](../architecture.md#notificación-en-tiempo-real-de-mejor-esfuerzo-la-fuente-de-verdad-es-la-api): resumen.
