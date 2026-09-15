using MentalHealthTracker.Domain.Enums;
using MentalHealthTracker.Domain.ValueObjects;

namespace MentalHealthTracker.Api.Modules.DailyLog.Dtos;

public sealed record CreateDailyLogRequest(
    DateOnly LogDate,
    short MoodRating,
    short AnxietyLevel,
    short StressLevel,
    decimal SleepHours,
    short SleepQuality,
    List<SleepDisturbance> SleepDisturbances,
    ActivityType? ActivityType,
    short? ActivityMinutes,
    SocialFrequency SocialFrequency,
    List<Symptom> Symptoms,
    string? Notes);