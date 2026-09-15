using MentalHealthTracker.Api.Modules.DailyLog.Dtos;

namespace MentalHealthTracker.Api.Modules.DailyLog;

public interface IDailyLogService
{
    Task<(DailyLogResponse Response, bool Created)> UpsertAsync(
        Guid userId,
        CreateDailyLogRequest request,
        CancellationToken cancellationToken = default);

    Task<DailyLogListResponse> ListAsync(
        Guid userId,
        ListDailyLogsQuery query,
        CancellationToken cancellationToken = default);

    Task<DailyLogResponse?> GetTodayAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}