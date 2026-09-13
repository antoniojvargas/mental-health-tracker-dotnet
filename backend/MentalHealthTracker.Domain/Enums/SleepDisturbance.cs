using System.Text.Json.Serialization;

namespace MentalHealthTracker.Domain.Enums;

[JsonConverter(typeof(SnakeCaseEnumJsonConverter))]
public enum SleepDisturbance
{
    None,
    Insomnia,
    Nightmares,
    FrequentWaking,
    EarlyWaking,
}