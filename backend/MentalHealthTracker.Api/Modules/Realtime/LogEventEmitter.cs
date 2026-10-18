using MentalHealthTracker.Api.Modules.DailyLog.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace MentalHealthTracker.Api.Modules.Realtime;

// El aviso en tiempo real es de mejor esfuerzo: cuando se emite, el registro ya está
// confirmado en la base de datos, así que un fallo del hub no puede convertir un guardado
// correcto en un 500. Se registra en Warning y no se relanza.
//
// El envío usa CancellationToken.None a propósito: si el cliente que guardó corta la
// conexión tras el commit, el token de la petición se cancela, y las demás pestañas de
// ese usuario deben enterarse igual. Por eso el token de la interfaz no se usa.
// OperationCanceledException sí se deja pasar: es un apagado del host, no un fallo del envío.
public sealed partial class LogEventEmitter(
    IHubContext<LogsHub> hubContext,
    ILogger<LogEventEmitter> logger) : ILogEventEmitter
{
    public Task EmitLogCreatedAsync(
        Guid userId,
        DailyLogResponse log,
        CancellationToken cancellationToken = default) =>
        SendAsync("log:created", userId, log);

    public Task EmitLogUpdatedAsync(
        Guid userId,
        DailyLogResponse log,
        CancellationToken cancellationToken = default) =>
        SendAsync("log:updated", userId, log);

    private async Task SendAsync(string eventName, Guid userId, DailyLogResponse log)
    {
        try
        {
            await hubContext.Clients
                .Group(LogsHub.GroupNameFor(userId))
                .SendAsync(eventName, log, CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogSendFailed(logger, exception, eventName, userId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not send realtime event {EventName} to user {UserId}")]
    private static partial void LogSendFailed(ILogger logger, Exception exception, string eventName, Guid userId);
}
