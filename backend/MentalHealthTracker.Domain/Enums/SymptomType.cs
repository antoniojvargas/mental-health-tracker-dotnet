using System.Text.Json.Serialization;

namespace MentalHealthTracker.Domain.Enums;

[JsonConverter(typeof(SnakeCaseEnumJsonConverter))]
public enum SymptomType
{
    LowMood,
    Hopelessness,
    Fatigue,
    Irritability,
    Panic,
    Restlessness,
    Concentration,
    AppetiteChange,
}