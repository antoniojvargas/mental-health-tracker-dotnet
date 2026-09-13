using System.Text.Json.Serialization;

namespace MentalHealthTracker.Domain.Enums;

[JsonConverter(typeof(SnakeCaseEnumJsonConverter))]
public enum ActivityType
{
    None,
    Walking,
    Running,
    Gym,
    Yoga,
    Cycling,
    Sports,
    Other,
}