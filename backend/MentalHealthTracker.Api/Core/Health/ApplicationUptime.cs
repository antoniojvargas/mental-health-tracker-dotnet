namespace MentalHealthTracker.Api.Core.Health;

// Instante de arranque de la aplicación; se registra como singleton para que el
// health check calcule el uptime sin depender de una variable local de Program.cs.
public sealed class ApplicationUptime(TimeProvider timeProvider)
{
    public DateTimeOffset StartedAt { get; } = timeProvider.GetUtcNow();

    public TimeSpan Elapsed => timeProvider.GetUtcNow() - StartedAt;
}
