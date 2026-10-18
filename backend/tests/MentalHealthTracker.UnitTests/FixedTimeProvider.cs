namespace MentalHealthTracker.UnitTests;

// Reloj de test con la hora fija y avance manual, para afirmar sobre instantes concretos
// sin depender de cuándo se ejecuta la prueba. Hereda de TimeProvider, así que se inyecta
// donde producción recibe TimeProvider.System.
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    // Instante por defecto de los tests: mediodía UTC, lejos de cualquier cambio de día.
    public static readonly DateTimeOffset NoonUtc = new(2030, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private DateTimeOffset _now = now;

    public FixedTimeProvider()
        : this(NoonUtc)
    {
    }

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
