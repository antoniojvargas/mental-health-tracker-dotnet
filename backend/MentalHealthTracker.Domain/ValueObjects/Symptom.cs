using MentalHealthTracker.Domain.Enums;

namespace MentalHealthTracker.Domain.ValueObjects;

public sealed record Symptom(SymptomType Type, int Severity);