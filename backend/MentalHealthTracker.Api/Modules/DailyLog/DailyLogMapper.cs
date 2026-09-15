using System.Globalization;
using MentalHealthTracker.Api.Modules.DailyLog.Dtos;
using MentalHealthTracker.Domain.Entities;

namespace MentalHealthTracker.Api.Modules.DailyLog;

public static class DailyLogMapper
{
    public static DailyLogResponse ToResponse(this DailyLog log) => new(
        Id: log.Id,
        LogDate: log.LogDate,
        MoodRating: log.MoodRating,
        AnxietyLevel: log.AnxietyLevel,
        StressLevel: log.StressLevel,
        SleepHours: (double)log.SleepHours,
        SleepQuality: log.SleepQuality,
        SleepDisturbances: log.SleepDisturbances,
        ActivityType: log.ActivityType,
        ActivityMinutes: log.ActivityMinutes,
        SocialFrequency: log.SocialFrequency,
        Symptoms: log.Symptoms,
        Notes: log.Notes,
        CreatedAt: log.CreatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        UpdatedAt: log.UpdatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));