using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace MentalHealthTracker.Api.Modules.Realtime;

// Rastrea las conexiones activas del hub para poder cerrarlas de forma ordenada
// durante el apagado: abortar cada HubCallerContext termina el WebSocket/request
// en vuelo, de modo que Kestrel no tiene que esperar el timeout de drenaje para
// liberar esas conexiones.
public sealed class SignalRConnectionTracker
{
    private readonly ConcurrentDictionary<string, HubCallerContext> _connections = new();

    public int Count => _connections.Count;

    public void Track(HubCallerContext context) => _connections[context.ConnectionId] = context;

    public void Untrack(string connectionId) => _connections.TryRemove(connectionId, out _);

    public void CloseAllConnections()
    {
        foreach (var context in _connections.Values)
        {
            context.Abort();
        }

        _connections.Clear();
    }
}