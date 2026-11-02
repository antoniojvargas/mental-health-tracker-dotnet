namespace MentalHealthTracker.Api.Core.RateLimiting;

// Contador de ventana fija compartido entre réplicas. Count es el valor tras sumar `by`;
// TimeToReset, lo que falta para que la ventana de esa clave se reinicie.
public readonly record struct RateLimitCount(long Count, TimeSpan TimeToReset);

public interface IRateLimitCounterStore
{
    // Suma `by` al contador de `key`; la primera suma de una ventana la abre con duración `window`.
    RateLimitCount Increment(string key, int by, TimeSpan window);

    Task<RateLimitCount> IncrementAsync(string key, int by, TimeSpan window, CancellationToken cancellationToken);
}
