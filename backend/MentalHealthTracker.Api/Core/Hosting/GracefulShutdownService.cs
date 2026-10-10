using MentalHealthTracker.Api.Modules.Realtime;
using Npgsql;

namespace MentalHealthTracker.Api.Core.Hosting;

// Coordina el apagado ordenado del backend: al llegar el SIGTERM (o cualquier
// detencion del host), cierra las conexiones de SignalR activas y libera el pool
// de Npgsql para que no queden recursos colgados. Kestrel deja de aceptar
// conexiones nuevas en cuanto arranca el shutdown, y tanto HostOptions como
// KestrelServerOptions limitan la espera a 10s: si algo se queda colgado, el
// proceso sale forzado por timeout.
public sealed class GracefulShutdownService(
    SignalRConnectionTracker signalRConnections,
    ILogger<GracefulShutdownService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Shutting down: closing {ConnectionCount} live SignalR connection(s)",
            signalRConnections.Count);
        signalRConnections.CloseAllConnections();

        logger.LogInformation("Shutting down: closing Npgsql connection pool");
        NpgsqlConnection.ClearAllPools();

        return Task.CompletedTask;
    }
}