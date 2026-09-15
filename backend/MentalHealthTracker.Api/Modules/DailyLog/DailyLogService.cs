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