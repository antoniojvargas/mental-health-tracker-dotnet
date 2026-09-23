using MentalHealthTracker.Api.Modules.DailyLog.Dtos;

namespace MentalHealthTracker.Api.Modules.Realtime;

public interface ILogEventEmitter
{
    Task EmitLogCreatedAsync(Guid userId, DailyLogResponse log, CancellationToken cancellationToken = default);

    Task EmitLogUpdatedAsync(Guid userId, DailyLogResponse log, CancellationToken cancellationToken = default);
}