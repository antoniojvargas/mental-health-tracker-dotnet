using MentalHealthTracker.Domain.Enums;
using MentalHealthTracker.Domain.ValueObjects;

namespace MentalHealthTracker.Api.Modules.DailyLog.Dtos;

public sealed record DailyLogResponse(
    Guid Id,
    DateOnly LogDate,
    short MoodRating,
    short AnxietyLevel,
    short StressLevel,
    double SleepHours,
    short SleepQuality,
    List<SleepDisturbance> SleepDisturbances,
    ActivityType? ActivityType,
    short? ActivityMinutes,
    SocialFrequency SocialFrequency,
    List<Symptom> Symptoms,
    string? Notes,
    string CreatedAt,
    string UpdatedAt);