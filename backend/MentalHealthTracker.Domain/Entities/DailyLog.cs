using MentalHealthTracker.Domain.Enums;
using MentalHealthTracker.Domain.ValueObjects;

namespace MentalHealthTracker.Domain.Entities;

public sealed class DailyLog
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DateOnly LogDate { get; set; }

    public short MoodRating { get; set; }

    public short AnxietyLevel { get; set; }

    public short StressLevel { get; set; }

    public decimal SleepHours { get; set; }

    public short SleepQuality { get; set; }

    public List<SleepDisturbance> SleepDisturbances { get; set; } = [];

    public ActivityType? ActivityType { get; set; }

    public short? ActivityMinutes { get; set; }

    public SocialFrequency SocialFrequency { get; set; }

    public List<Symptom> Symptoms { get; set; } = [];

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}