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
        var service = new DailyLogService(repository, emitter);

        await service.UpsertAsync(UserId, ValidRequest(), CancellationToken.None);

        Assert.Equal(expectsCreatedEvent, emitter.CreatedEmitted);
        Assert.Equal(!expectsCreatedEvent, emitter.UpdatedEmitted);
    }

    [Fact]
    public async Task Upsert_CreatesLog_EmitsCompleteDto()
    {
        var emitter = new FakeLogEventEmitter();
        var repository = new FakeDailyLogRepository(created: true);
        var service = new DailyLogService(repository, emitter);
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
        var service = new DailyLogService(repository, emitter);
        var request = ValidRequest();

        await service.UpsertAsync(UserId, request, CancellationToken.None);

        var emitted = Assert.Single(emitter.EmittedLogs);
        Assert.Equal(UserId, emitter.EmittedUserId);
        Assert.Equal(ExpectedDto(request), emitted);
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
        CreatedAt = DateTimeOffset.Parse("2026-09-22T10:00:00+00:00"),
        UpdatedAt = DateTimeOffset.Parse("2026-09-22T11:00:00+00:00"),
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
        CreatedAt: DateTimeOffset.Parse("2026-09-22T10:00:00+00:00").ToUniversalTime().ToString("O"),
        UpdatedAt: DateTimeOffset.Parse("2026-09-22T11:00:00+00:00").ToUniversalTime().ToString("O"));

    private sealed class FakeDailyLogRepository(bool created) : IDailyLogRepository
    {
        public Task<DailyLog?> FindByUserAndDateAsync(
            Guid userId, DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult<DailyLog?>(null);

        public Task<PagedResult<DailyLog>> FindByUserAndRangeAsync(
            Guid userId,
            DateOnly from,
            DateOnly to,
            int limit,
            int offset,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<DailyLog>([], 0));

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