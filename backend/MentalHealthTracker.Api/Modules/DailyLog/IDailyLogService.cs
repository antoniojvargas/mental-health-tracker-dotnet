using MentalHealthTracker.Api.Modules.DailyLog.Dtos;

namespace MentalHealthTracker.Api.Modules.DailyLog;

public interface IDailyLogService
{
    Task<(DailyLogResponse Response, bool Created)> UpsertAsync(
        Guid userId,
        CreateDailyLogRequest request,
        CancellationToken cancellationToken = default);
}