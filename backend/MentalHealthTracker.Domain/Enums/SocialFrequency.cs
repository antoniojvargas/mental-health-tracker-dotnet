using System.Text.Json.Serialization;

namespace MentalHealthTracker.Domain.Enums;

[JsonConverter(typeof(SnakeCaseEnumJsonConverter))]
public enum SocialFrequency
{
    None,
    Rare,
    Occasional,
    Frequent,
    Daily,
}