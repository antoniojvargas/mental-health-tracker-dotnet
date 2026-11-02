using System.Threading.RateLimiting;
using MentalHealthTracker.Api.Core.RateLimiting;
using Microsoft.Extensions.Logging;

namespace MentalHealthTracker.UnitTests;

// El limitador distribuido cuenta en un almacén compartido y mantiene la semántica del de ventana
// fija: PermitLimit peticiones por ventana y, al pasarse, un rechazo con RETRY_AFTER. Si el almacén
// falla deja pasar y lo registra.
public sealed class DistributedFixedWindowRateLimiterTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    [Fact]
    public async Task Async_AllowsUpToTheLimitAndThenRejectsWithRetryAfter()
    {
        var store = new CountingStore { TimeToReset = TimeSpan.FromSeconds(42) };
        using var limiter = NewLimiter(store, 3);

        for (var i = 0; i < 3; i++)
        {
            Assert.True((await limiter.AcquireAsync()).IsAcquired);
        }

        var rejected = await limiter.AcquireAsync();

        Assert.False(rejected.IsAcquired);
        Assert.True(rejected.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter));
        Assert.Equal(TimeSpan.FromSeconds(42), retryAfter);
    }

    [Fact]
    public void Sync_AllowsUpToTheLimitAndThenRejects()
    {
        using var limiter = NewLimiter(new CountingStore(), 2);

        Assert.True(limiter.AttemptAcquire().IsAcquired);
        Assert.True(limiter.AttemptAcquire().IsAcquired);
        Assert.False(limiter.AttemptAcquire().IsAcquired);
    }

    [Fact]
    public async Task AnAcquiredLease_CarriesNoRetryAfter()
    {
        using var limiter = NewLimiter(new CountingStore(), 1);

        var lease = await limiter.AcquireAsync();

        Assert.True(lease.IsAcquired);
        Assert.Empty(lease.MetadataNames);
        Assert.False(lease.TryGetMetadata(MetadataName.RetryAfter, out _));
    }

    [Fact]
    public async Task ItCountsUnderTheGivenKeyAndWindow()
    {
        var store = new CountingStore();
        using var limiter = NewLimiter(store, 5, key: "mht:rl:auth:203.0.113.1");

        await limiter.AcquireAsync(2);

        var call = Assert.Single(store.Calls);
        Assert.Equal("mht:rl:auth:203.0.113.1", call.Key);
        Assert.Equal(2, call.By);
        Assert.Equal(Window, call.Window);
    }

    [Fact]
    public async Task ZeroPermits_DoesNotTouchTheStore()
    {
        var store = new CountingStore();
        using var limiter = NewLimiter(store, 1);

        Assert.True((await limiter.AcquireAsync(0)).IsAcquired);
        Assert.Empty(store.Calls);
    }

    [Fact]
    public async Task MoreThanTheLimitInOneRequest_IsAnArgumentError()
    {
        using var limiter = NewLimiter(new CountingStore(), 2);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await limiter.AcquireAsync(3));
    }

    [Fact]
    public async Task WhenTheStoreFails_RequestsPassAndTheFailureIsLoggedAsAWarning()
    {
        var logger = new CapturingLogger();
        using var limiter = NewLimiter(new CountingStore { Fail = true }, 1, logger: logger);

        Assert.True((await limiter.AcquireAsync()).IsAcquired);
        Assert.True(limiter.AttemptAcquire().IsAcquired);
        Assert.Equal(2, logger.Warnings);
    }

    [Fact]
    public async Task ACancelledRequest_IsNotTreatedAsAStoreOutage()
    {
        var logger = new CapturingLogger();
        using var limiter = NewLimiter(new CountingStore { Cancel = true }, 1, logger: logger);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await limiter.AcquireAsync());
        Assert.Equal(0, logger.Warnings);
    }

    [Fact]
    public void ItKeepsNoLocalState_SoThePartitionedLimiterCanDiscardIt()
    {
        using var limiter = NewLimiter(new CountingStore(), 1);

        Assert.Equal(TimeSpan.MaxValue, limiter.IdleDuration);
        Assert.Null(limiter.GetStatistics());
    }

    private static DistributedFixedWindowRateLimiter NewLimiter(
        IRateLimitCounterStore store, int limit, string key = "k", ILogger? logger = null) =>
        new(store, key, limit, Window, logger ?? new CapturingLogger());

    private sealed class CountingStore : IRateLimitCounterStore
    {
        private long _count;

        public List<(string Key, int By, TimeSpan Window)> Calls { get; } = [];

        public TimeSpan TimeToReset { get; init; } = TimeSpan.FromMinutes(1);

        public bool Fail { get; init; }

        public bool Cancel { get; init; }

        public RateLimitCount Increment(string key, int by, TimeSpan window)
        {
            if (Fail)
            {
                throw new InvalidOperationException("store down");
            }

            if (Cancel)
            {
                throw new OperationCanceledException();
            }

            Calls.Add((key, by, window));
            _count += by;
            return new RateLimitCount(_count, TimeToReset);
        }

        public Task<RateLimitCount> IncrementAsync(string key, int by, TimeSpan window, CancellationToken cancellationToken) =>
            Task.FromResult(Increment(key, by, window));
    }

    private sealed class CapturingLogger : ILogger
    {
        public int Warnings { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings++;
            }
        }
    }
}
