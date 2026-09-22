using MentalHealthTracker.Api.Modules.DailyLog.Dtos;
using MentalHealthTracker.Api.Modules.Realtime;
using MentalHealthTracker.Domain.Repositories;
using DailyLogEntity = MentalHealthTracker.Domain.Entities.DailyLog;

namespace MentalHealthTracker.Api.Modules.DailyLog;

public sealed class DailyLogService(
    IDailyLogRepository repository,
    ILogEventEmitter logEventEmitter) : IDailyLogService
{
    public async Task<(DailyLogResponse Response, bool Created)> UpsertAsync(
        Guid userId,
        CreateDailyLogRequest request,
        CancellationToken cancellationToken = default)
    {
        var log = MapToEntity(userId, request);
        var result = await repository.UpsertAsync(log, cancellationToken);

        var response = result.Log.ToResponse();

        if (result.Created)
        {
            await logEventEmitter.EmitLogCreatedAsync(userId, response, cancellationToken);
        }
        else
        {
            await logEventEmitter.EmitLogUpdatedAsync(userId, response, cancellationToken);
        }

        return (response, result.Created);
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

    public async Task<DailyLogResponse?> GetTodayAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var log = await repository.FindByUserAndDateAsync(userId, today, cancellationToken);

        return log?.ToResponse();
    }

    private static DailyLogEntity MapToEntity(Guid userId, CreateDailyLogRequest request) => new()
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