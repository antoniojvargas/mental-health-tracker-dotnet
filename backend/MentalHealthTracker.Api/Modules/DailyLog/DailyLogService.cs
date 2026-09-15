using MentalHealthTracker.Api.Modules.DailyLog.Dtos;
using MentalHealthTracker.Domain.Entities;
using MentalHealthTracker.Domain.Repositories;

namespace MentalHealthTracker.Api.Modules.DailyLog;

public sealed class DailyLogService(IDailyLogRepository repository) : IDailyLogService
{
    public async Task<(DailyLogResponse Response, bool Created)> UpsertAsync(
        Guid userId,
        CreateDailyLogRequest request,
        CancellationToken cancellationToken = default)
    {
        var log = MapToEntity(userId, request);
        var result = await repository.UpsertAsync(log, cancellationToken);

        return (result.Log.ToResponse(), result.Created);
    }

    public async Task<DailyLogListResponse> ListAsync(
        Guid userId,
        ListDailyLogsQuery query,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = query.From ?? today.AddDays(-29);
        var to = query.To ?? today;

        var result = await repository.FindByUserAndRangeAsync(
            userId, from, to, query.Limit, query.Offset, cancellationToken);

        return new DailyLogListResponse(
            result.Items.Select(item => item.ToResponse()).ToArray(),
            new DailyLogListMeta(from, to, query.Limit, query.Offset, result.Total));
    }

    private static DailyLog MapToEntity(Guid userId, CreateDailyLogRequest request) => new()
    {
        UserId = userId,
        LogDate = request.LogDate,
        MoodRating = request.MoodRating,
        AnxietyLevel = request.AnxietyLevel,
        StressLevel = request.StressLevel,
        SleepHours = request.SleepHours,
        SleepQuality = request.SleepQuality,
        SleepDisturbances = request.SleepDisturbances,
        ActivityType = request.ActivityType,
        ActivityMinutes = request.ActivityMinutes,
        SocialFrequency = request.SocialFrequency,
        Symptoms = request.Symptoms,
        Notes = request.Notes,
    };
}