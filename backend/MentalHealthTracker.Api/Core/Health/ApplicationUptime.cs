namespace MentalHealthTracker.Api.Core.Health;

// Instante de arranque de la aplicación; se registra como singleton para que el
// health check calcule el uptime sin depender de una variable local de Program.cs.
public sealed class ApplicationUptime
{
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;

    public TimeSpan Elapsed => DateTimeOffset.UtcNow - StartedAt;
}
