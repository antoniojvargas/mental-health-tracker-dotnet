using System.Globalization;
using MentalHealthTracker.Api.Modules.DailyLog;
using MentalHealthTracker.Api.Modules.DailyLog.Dtos;
using MentalHealthTracker.Api.Modules.Realtime;
using MentalHealthTracker.Domain.Entities;
using MentalHealthTracker.Domain.Enums;
using MentalHealthTracker.Domain.Models;
using MentalHealthTracker.Domain.Repositories;
using MentalHealthTracker.Domain.ValueObjects;

namespace MentalHealthTracker.UnitTests;

public class DailyLogServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Upsert_EmitsEventForOutcome(bool created, bool expectsCreatedEvent)
    {
        var emitter = new FakeLogEventEmitter();
        var repository = new FakeDailyLogRepository(created);
        var service = new DailyLogService(repository, emitter, new FixedTimeProvider());

        await service.UpsertAsync(UserId, ValidRequest(), CancellationToken.None);

        Assert.Equal(expectsCreatedEvent, emitter.CreatedEmitted);
        Assert.Equal(!expectsCreatedEvent, emitter.UpdatedEmitted);
    }

    [Fact]
    public async Task Upsert_CreatesLog_EmitsCompleteDto()
    {
        var emitter = new FakeLogEventEmitter();
        var repository = new FakeDailyLogRepository(created: true);
        var service = new DailyLogService(repository, emitter, new FixedTimeProvider());
        var request = ValidRequest();

        await service.UpsertAsync(UserId, request, CancellationToken.None);

        var emitted = Assert.Single(emitter.EmittedLogs);
        Assert.Equal(UserId, emitter.EmittedUserId);
        Assert.Equal(ExpectedDto(request), emitted);
    }

    [Fact]
    public async Task Upsert_UpdatesLog_EmitsCompleteDto()
    {
        var emitter = new FakeLogEventEmitter();
        var repository = new FakeDailyLogRepository(created: false);
        var service = new DailyLogService(repository, emitter, new FixedTimeProvider());
        var request = ValidRequest();

        await service.UpsertAsync(UserId, request, CancellationToken.None);

        var emitted = Assert.Single(emitter.EmittedLogs);
        Assert.Equal(UserId, emitter.EmittedUserId);
        Assert.Equal(ExpectedDto(request), emitted);
    }

    // El rango por defecto y el "hoy" por defecto salen del día UTC del reloj, no de la hora local.
    [Theory]
    [InlineData("2030-06-15T12:00:00+00:00", "2030-05-17", "2030-06-15")]
    [InlineData("2030-06-15T23:59:59.9999999+00:00", "2030-05-17", "2030-06-15")]
    [InlineData("2030-06-16T00:00:00+00:00", "2030-05-18", "2030-06-16")]
    public async Task List_WithoutRange_DefaultsToTheLast30UtcDaysEndingToday(string now, string expectedFrom, string expectedTo)
    {
        var repository = new FakeDailyLogRepository(created: true);
        var service = new DailyLogService(
            repository,
            new FakeLogEventEmitter(),
            new FixedTimeProvider(DateTimeOffset.Parse(now, CultureInfo.InvariantCulture)));

        var response = await service.ListAsync(UserId, new ListDailyLogsQuery(), CancellationToken.None);

        var (from, to) = Assert.Single(repository.RangesQueried);
        Assert.Equal(DateOnly.Parse(expectedFrom, CultureInfo.InvariantCulture), from);
        Assert.Equal(DateOnly.Parse(expectedTo, CultureInfo.InvariantCulture), to);
        Assert.Equal(from, response.Meta.From);
        Assert.Equal(to, response.Meta.To);
        Assert.Equal(29, to.DayNumber - from.DayNumber);
    }

    [Fact]
    public async Task List_WithExplicitRange_IgnoresTheClock()
    {
        var repository = new FakeDailyLogRepository(created: true);
        var service = new DailyLogService(repository, new FakeLogEventEmitter(), new FixedTimeProvider());
        var query = new ListDailyLogsQuery(new DateOnly(2030, 1, 1), new DateOnly(2030, 1, 10));

        await service.ListAsync(UserId, query, CancellationToken.None);

        Assert.Equal((new DateOnly(2030, 1, 1), new DateOnly(2030, 1, 10)), Assert.Single(repository.RangesQueried));
    }

    [Theory]
    [InlineData("2030-06-15T23:59:59.9999999+00:00", "2030-06-15")]
    [InlineData("2030-06-16T00:00:00+00:00", "2030-06-16")]
    public async Task GetToday_WithoutDate_UsesTheUtcDayOfTheClock(string now, string expectedDay)
    {
        var repository = new FakeDailyLogRepository(created: true);
        var service = new DailyLogService(
            repository,
            new FakeLogEventEmitter(),
            new FixedTimeProvider(DateTimeOffset.Parse(now, CultureInfo.InvariantCulture)));

        await service.GetTodayAsync(UserId, null, CancellationToken.None);

        Assert.Equal(DateOnly.Parse(expectedDay, CultureInfo.InvariantCulture), Assert.Single(repository.DatesQueried));
    }

    [Fact]
    public async Task GetToday_WithClientDate_UsesThatDate()
    {
        var repository = new FakeDailyLogRepository(created: true);
        var service = new DailyLogService(repository, new FakeLogEventEmitter(), new FixedTimeProvider());

        await service.GetTodayAsync(UserId, new DateOnly(2030, 6, 16), CancellationToken.None);

        Assert.Equal(new DateOnly(2030, 6, 16), Assert.Single(repository.DatesQueried));
    }

    private static CreateDailyLogRequest ValidRequest() => new(
        LogDate: new DateOnly(2026, 9, 22),
        MoodRating: 3,
        AnxietyLevel: 5,
        StressLevel: 5,
        SleepHours: 7.5m,
        SleepQuality: 4,
        SleepDisturbances: [SleepDisturbance.FrequentWaking],
        ActivityType: ActivityType.Walking,
        ActivityMinutes: 30,
        SocialFrequency: SocialFrequency.Occasional,
        Symptoms: [new Symptom(SymptomType.Fatigue, 3)],
        Notes: "mejor que ayer");

    private static DailyLog ExpectedEntity() => new()
    {
        Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
        UserId = UserId,
        LogDate = new DateOnly(2026, 9, 22),
        MoodRating = 3,
        AnxietyLevel = 5,
        StressLevel = 5,
        SleepHours = 7.5m,
        SleepQuality = 4,
        SleepDisturbances = [SleepDisturbance.FrequentWaking],
        ActivityType = ActivityType.Walking,
        ActivityMinutes = 30,
        SocialFrequency = SocialFrequency.Occasional,
        Symptoms = [new Symptom(SymptomType.Fatigue, 3)],
        Notes = "mejor que ayer",
        CreatedAt = DateTimeOffset.Parse("2026-09-22T10:00:00+00:00", CultureInfo.InvariantCulture),
        UpdatedAt = DateTimeOffset.Parse("2026-09-22T11:00:00+00:00", CultureInfo.InvariantCulture),
    };

    private static DailyLogResponse ExpectedDto(CreateDailyLogRequest request) => new(
        Id: Guid.Parse("00000000-0000-0000-0000-000000000001"),
        LogDate: new DateOnly(2026, 9, 22),
        MoodRating: 3,
        AnxietyLevel: 5,
        StressLevel: 5,
        SleepHours: 7.5,
        SleepQuality: 4,
        SleepDisturbances: request.SleepDisturbances,
        ActivityType: ActivityType.Walking,
        ActivityMinutes: 30,
        SocialFrequency: SocialFrequency.Occasional,
        Symptoms: request.Symptoms,
        Notes: "mejor que ayer",
        CreatedAt: DateTimeOffset.Parse("2026-09-22T10:00:00+00:00", CultureInfo.InvariantCulture).ToUniversalTime().ToString("O"),
        UpdatedAt: DateTimeOffset.Parse("2026-09-22T11:00:00+00:00", CultureInfo.InvariantCulture).ToUniversalTime().ToString("O"));

    private sealed class FakeDailyLogRepository(bool created) : IDailyLogRepository
    {
        public List<DateOnly> DatesQueried { get; } = [];

        public List<(DateOnly From, DateOnly To)> RangesQueried { get; } = [];

        public Task<DailyLog?> FindByUserAndDateAsync(
            Guid userId, DateOnly logDate, CancellationToken cancellationToken = default)
        {
            DatesQueried.Add(logDate);
            return Task.FromResult<DailyLog?>(null);
        }

        public Task<PagedResult<DailyLog>> FindByUserAndRangeAsync(
            Guid userId,
            DateOnly fromDate,
            DateOnly toDate,
            int limit,
            int offset,
            CancellationToken cancellationToken = default)
        {
            RangesQueried.Add((fromDate, toDate));
            return Task.FromResult(new PagedResult<DailyLog>([], 0));
        }

        public Task<UpsertedDailyLog> UpsertAsync(
            DailyLog log, CancellationToken cancellationToken = default)
        {
            log.Id = ExpectedEntity().Id;
            log.CreatedAt = ExpectedEntity().CreatedAt;
            log.UpdatedAt = ExpectedEntity().UpdatedAt;
            return Task.FromResult(new UpsertedDailyLog(log, created));
        }
    }

    private sealed class FakeLogEventEmitter : ILogEventEmitter
    {
        public bool CreatedEmitted { get; private set; }

        public bool UpdatedEmitted { get; private set; }

        public Guid EmittedUserId { get; private set; }

        public List<DailyLogResponse> EmittedLogs { get; } = [];

        public Task EmitLogCreatedAsync(
            Guid userId, DailyLogResponse log, CancellationToken cancellationToken = default)
        {
            CreatedEmitted = true;
            Record(userId, log);
            return Task.CompletedTask;
        }

        public Task EmitLogUpdatedAsync(
            Guid userId, DailyLogResponse log, CancellationToken cancellationToken = default)
        {
            UpdatedEmitted = true;
            Record(userId, log);
            return Task.CompletedTask;
        }

        private void Record(Guid userId, DailyLogResponse log)
        {
            EmittedUserId = userId;
            EmittedLogs.Add(log);
        }
    }
}