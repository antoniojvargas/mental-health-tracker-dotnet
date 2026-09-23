using MentalHealthTracker.Api.Modules.DailyLog.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace MentalHealthTracker.Api.Modules.Realtime;

public sealed class LogEventEmitter(IHubContext<LogsHub> hubContext) : ILogEventEmitter
{
    public async Task EmitLogCreatedAsync(
        Guid userId,
        DailyLogResponse log,
        CancellationToken cancellationToken = default)
    {
        await hubContext.Clients
            .Group(LogsHub.GroupNameFor(userId))
            .SendAsync("log:created", log, cancellationToken);
    }

    public async Task EmitLogUpdatedAsync(
        Guid userId,
        DailyLogResponse log,
        CancellationToken cancellationToken = default)
    {
        await hubContext.Clients
            .Group(LogsHub.GroupNameFor(userId))
            .SendAsync("log:updated", log, cancellationToken);
    }
}