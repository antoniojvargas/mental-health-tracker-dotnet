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
    public async Task Emit_SendsTheLogToTheUsersGroupUnderTheEventName(string eventName)
    {
        var hub = new FakeHub(null);
        var emitter = new LogEventEmitter(hub, new CapturingLogger());
        var log = Log();

        await Emit(emitter, eventName, CancellationToken.None, log);

        Assert.Equal($"user:{UserId}", hub.SentGroup);
        Assert.Equal(eventName, hub.SentMethod);
        Assert.Same(log, Assert.Single(hub.SentArgs));
    }

    [Fact]
    public async Task Emit_OnlyReachesTheGroupOfTheUserWhoOwnsTheLog()
    {
        var hub = new FakeHub(null);
        var emitter = new LogEventEmitter(hub, new CapturingLogger());
        var other = Guid.NewGuid();

        await emitter.EmitLogCreatedAsync(other, Log());

        Assert.Equal($"user:{other}", hub.SentGroup);
        Assert.NotEqual($"user:{UserId}", hub.SentGroup);
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

    private static Task Emit(
        LogEventEmitter emitter,
        string eventName,
        CancellationToken token,
        DailyLogResponse? log = null) =>
        eventName == LogHubEvents.LogCreated
            ? emitter.EmitLogCreatedAsync(UserId, log ?? Log(), token)
            : emitter.EmitLogUpdatedAsync(UserId, log ?? Log(), token);

    private static DailyLogResponse Log() => new(
        Guid.NewGuid(),
        new DateOnly(2026, 10, 21),
        MoodRating: 3,
        AnxietyLevel: 4,
        StressLevel: 5,
        SleepHours: 7.5,
        SleepQuality: 3,
        SleepDisturbances: [],
        ActivityType: null,
        ActivityMinutes: null,
        SocialFrequency: default,
        Symptoms: [],
        Notes: null,
        CreatedAt: "2026-10-21T08:00:00Z",
        UpdatedAt: "2026-10-21T08:00:00Z");

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

    private sealed class FakeHub : IHubContext<LogsHub>
    {
        private readonly Proxy _proxy;
        private readonly HubClients _clients;

        public FakeHub(Exception? toThrow)
        {
            _proxy = new Proxy(toThrow);
            _clients = new HubClients(_proxy);
        }

        public string? SentMethod => _proxy.SentMethod;

        public CancellationToken SentToken => _proxy.SentToken;

        public object?[] SentArgs => _proxy.SentArgs;

        public string? SentGroup => _clients.RequestedGroup;

        public IHubClients Clients => _clients;

        public IGroupManager Groups => throw new NotSupportedException();
    }

    // Solo Group(...) es un destino válido: cualquier otro (todos, un usuario, una conexión) rompe el test.
    private sealed class HubClients(IClientProxy proxy) : IHubClients
    {
        public string? RequestedGroup { get; private set; }

        public IClientProxy Group(string groupName)
        {
            RequestedGroup = groupName;
            return proxy;
        }

        public IClientProxy All => throw new NotSupportedException();

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

        public IClientProxy Client(string connectionId) => throw new NotSupportedException();

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();

        public IClientProxy User(string userId) => throw new NotSupportedException();

        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class Proxy(Exception? toThrow) : IClientProxy
    {
        public string? SentMethod { get; private set; }

        public CancellationToken SentToken { get; private set; }

        public object?[] SentArgs { get; private set; } = [];

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            SentMethod = method;
            SentArgs = args;
            SentToken = cancellationToken;
            return toThrow is null ? Task.CompletedTask : Task.FromException(toThrow);
        }
    }
}
