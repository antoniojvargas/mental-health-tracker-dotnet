using System.Threading.RateLimiting;
using MentalHealthTracker.Api.Core.Logging;

namespace MentalHealthTracker.Api.Core.RateLimiting;

// Limitador de ventana fija cuyo contador vive en un almacén compartido (Redis): con varias réplicas
// la cuota es una sola, no una por instancia. Misma semántica que FixedWindowRateLimiter: la ventana se
// abre con la primera petición y la cuota se agota al pasar PermitLimit. Mantiene el contrato HTTP: el
// rechazo lleva RETRY_AFTER, que OnRejected traduce a RateLimit-Reset / Retry-After.
//
// Si el almacén no responde se DEJA PASAR (fail-open) y se registra un aviso: una caída de Redis no
// debe impedir iniciar sesión ni guardar un registro. Es el mismo límite laxo que ya tenía el sistema
// sin cuotas compartidas, no un agujero nuevo.
public sealed partial class DistributedFixedWindowRateLimiter(
    IRateLimitCounterStore store,
    string key,
    int permitLimit,
    TimeSpan window,
    ILogger logger) : RateLimiter
{
    // El limitador no guarda estado local (el contador está en Redis), así que se declara ocioso desde
    // el principio: el limitador particionado descarta y recrea estos objetos sin perder nada.
    public override TimeSpan? IdleDuration => TimeSpan.MaxValue;

    public override RateLimiterStatistics? GetStatistics() => null;

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        if (permitCount == 0)
        {
            return DistributedLease.Acquired;
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(permitCount, permitLimit);

        try
        {
            return ToLease(store.Increment(key, permitCount, window));
        }
        catch (Exception exception) when (IsStoreFailure(exception))
        {
            LogStoreUnavailable(logger, exception, key);
            return DistributedLease.Acquired;
        }
    }

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        if (permitCount == 0)
        {
            return DistributedLease.Acquired;
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(permitCount, permitLimit);

        try
        {
            return ToLease(await store.IncrementAsync(key, permitCount, window, cancellationToken));
        }
        catch (Exception exception) when (IsStoreFailure(exception))
        {
            LogStoreUnavailable(logger, exception, key);
            return DistributedLease.Acquired;
        }
    }

    // Cualquier fallo del almacén menos la cancelación de quien pide (el cliente se fue): esa no es
    // una caída de Redis y se propaga.
    private static bool IsStoreFailure(Exception exception) => exception is not OperationCanceledException;

    private DistributedLease ToLease(RateLimitCount count) =>
        count.Count <= permitLimit
            ? DistributedLease.Acquired
            : new DistributedLease(isAcquired: false, retryAfter: count.TimeToReset);

    [LoggerMessage(EventId = ApiLogEvents.RateLimitStoreUnavailable, EventName = nameof(ApiLogEvents.RateLimitStoreUnavailable), Level = LogLevel.Warning, Message = "Rate limit store unavailable for {Key}; letting the request through")]
    private static partial void LogStoreUnavailable(ILogger logger, Exception exception, string key);

    private sealed class DistributedLease(bool isAcquired, TimeSpan? retryAfter = null) : RateLimitLease
    {
        public static readonly DistributedLease Acquired = new(isAcquired: true);

        public override bool IsAcquired => isAcquired;

        public override IEnumerable<string> MetadataNames =>
            retryAfter is null ? [] : [MetadataName.RetryAfter.Name];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (retryAfter is { } value && metadataName == MetadataName.RetryAfter.Name)
            {
                metadata = value;
                return true;
            }

            metadata = null;
            return false;
        }
    }
}
