namespace MentalHealthTracker.UnitTests;

// Reloj de test con la hora fija y avance manual, para afirmar sobre instantes concretos
// sin depender de cuándo se ejecuta la prueba.
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
