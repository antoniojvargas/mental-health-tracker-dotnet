using MentalHealthTracker.Api.Modules.DailyLog.Dtos;
using MentalHealthTracker.Api.Modules.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace MentalHealthTracker.UnitTests;

public sealed class LogEventEmitterTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    // Los nombres viajan por el cable y el frontend se suscribe a estas cadenas: se fijan literales.
    [Fact]
    public void EventNames_AreTheOnesTheFrontendSubscribesTo()
    {
        Assert.Equal("log:created", LogHubEvents.LogCreated);
        Assert.Equal("log:updated", LogHubEvents.LogUpdated);
    }

    [Theory]
    [InlineData(LogHubEvents.LogCreated)]
    [InlineData(LogHubEvents.LogUpdated)]
    public async Task Emit_WhenSendThrows_LogsWarningWithEventAndUserAndDoesNotRethrow(string eventName)
    {
        var logger = new CapturingLogger();
        var emitter = new LogEventEmitter(new FakeHub(new InvalidOperationException("hub is down")), logger);

        await Emit(emitter, eventName, CancellationToken.None);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.Contains(eventName, entry.Message);
        Assert.Contains(UserId.ToString(), entry.Message);
    }

    [Fact]
    public async Task Emit_WhenSendIsCancelled_Rethrows()
    {
        var logger = new CapturingLogger();
        var emitter = new LogEventEmitter(new FakeHub(new OperationCanceledException()), logger);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => emitter.EmitLogCreatedAsync(UserId, Log(), CancellationToken.None));
        Assert.Empty(logger.Entries);
    }

    [Theory]
    [InlineData(LogHubEvents.LogCreated)]
    [InlineData(LogHubEvents.LogUpdated)]
    public async Task Emit_WhenCallerTokenIsAlreadyCancelled_StillSendsWithNoneToken(string eventName)
    {
        var hub = new FakeHub(null);
        var emitter = new LogEventEmitter(hub, new CapturingLogger());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Emit(emitter, eventName, cancelled.Token);

        Assert.Equal(eventName, hub.SentMethod);
        Assert.Equal(CancellationToken.None, hub.SentToken);
    }

    private static Task Emit(LogEventEmitter emitter, string eventName, CancellationToken token) =>
        eventName == LogHubEvents.LogCreated
            ? emitter.EmitLogCreatedAsync(UserId, Log(), token)
            : emitter.EmitLogUpdatedAsync(UserId, Log(), token);

    private static DailyLogResponse Log() => (DailyLogResponse)System.Runtime.CompilerServices
        .RuntimeHelpers.GetUninitializedObject(typeof(DailyLogResponse));

    private sealed record Entry(LogLevel Level, string Message, Exception? Exception);

    private sealed class CapturingLogger : ILogger<LogEventEmitter>
    {
        public List<Entry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new Entry(logLevel, formatter(state, exception), exception));
    }

    private sealed class FakeHub(Exception? toThrow) : IHubContext<LogsHub>
    {
        private readonly Proxy _proxy = new(toThrow);

        public string? SentMethod => _proxy.SentMethod;

        public CancellationToken SentToken => _proxy.SentToken;

        public IHubClients Clients => new HubClients(_proxy);

        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class HubClients(IClientProxy proxy) : IHubClients
    {
        public IClientProxy All => proxy;

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => proxy;

        public IClientProxy Client(string connectionId) => proxy;

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => proxy;

        public IClientProxy Group(string groupName) => proxy;

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => proxy;

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => proxy;

        public IClientProxy User(string userId) => proxy;

        public IClientProxy Users(IReadOnlyList<string> userIds) => proxy;
    }

    private sealed class Proxy(Exception? toThrow) : IClientProxy
    {
        public string? SentMethod { get; private set; }

        public CancellationToken SentToken { get; private set; }

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            SentMethod = method;
            SentToken = cancellationToken;
            return toThrow is null ? Task.CompletedTask : Task.FromException(toThrow);
        }
    }
}
