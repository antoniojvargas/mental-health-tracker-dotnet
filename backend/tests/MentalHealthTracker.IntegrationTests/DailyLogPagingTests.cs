using System.Diagnostics;
using MentalHealthTracker.Domain.Entities;
using MentalHealthTracker.Domain.Enums;
using MentalHealthTracker.Domain.Models;
using MentalHealthTracker.Domain.Repositories;
using MentalHealthTracker.Domain.ValueObjects;
using MentalHealthTracker.Infrastructure.Persistence;
using MentalHealthTracker.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealthTracker.IntegrationTests;

// FindByUserAndRangeAsync devuelve la página y el total del rango en una sola consulta
// (COUNT(*) OVER()). Cuando la página llega vacía el total no viaja con ninguna fila, así que
// se pide con un COUNT de respaldo: meta.total debe ser el mismo en todos los casos.
[Collection(IntegrationTestGroup.Name)]
public sealed class DailyLogPagingTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory), IClassFixture<CustomWebApplicationFactory>
{
    private const int SeededDays = 25;

    private static readonly DateOnly FirstDay = new(2030, 6, 1);

    private static readonly DateOnly LastDay = FirstDay.AddDays(SeededDays - 1);

    [Fact]
    public async Task FullPage_ReturnsLimitItemsAndTheTotalOfTheRange()
    {
        var userId = await SeedUserWithLogsAsync();

        var page = await QueryAsync(userId, FirstDay, LastDay, limit: 10, offset: 0);

        Assert.Equal(10, page.Items.Count);
        Assert.Equal(SeededDays, page.Total);
        Assert.Equal(FirstDay, page.Items[0].LogDate);
        Assert.Equal(FirstDay.AddDays(9), page.Items[^1].LogDate);
    }

    [Fact]
    public async Task PartialPage_ReturnsTheRemainingItemsAndTheSameTotal()
    {
        var userId = await SeedUserWithLogsAsync();

        var page = await QueryAsync(userId, FirstDay, LastDay, limit: 10, offset: 20);

        Assert.Equal(5, page.Items.Count);
        Assert.Equal(SeededDays, page.Total);
        Assert.Equal(FirstDay.AddDays(20), page.Items[0].LogDate);
        Assert.Equal(LastDay, page.Items[^1].LogDate);
    }

    [Fact]
    public async Task EmptyPageBeyondTheRange_StillReportsTheTotal()
    {
        var userId = await SeedUserWithLogsAsync();

        var page = await QueryAsync(userId, FirstDay, LastDay, limit: 10, offset: 30);

        Assert.Empty(page.Items);
        Assert.Equal(SeededDays, page.Total);
    }

    [Fact]
    public async Task EmptyPageWhenTheOffsetIsExactlyTheTotal_StillReportsTheTotal()
    {
        var userId = await SeedUserWithLogsAsync();

        var page = await QueryAsync(userId, FirstDay, LastDay, limit: 10, offset: SeededDays);

        Assert.Empty(page.Items);
        Assert.Equal(SeededDays, page.Total);
    }

    [Fact]
    public async Task ALimitOfZero_IsAnEmptyPageWithTheRealTotal()
    {
        var userId = await SeedUserWithLogsAsync();

        var page = await QueryAsync(userId, FirstDay, LastDay, limit: 0, offset: 0);

        Assert.Empty(page.Items);
        Assert.Equal(SeededDays, page.Total);
    }

    [Fact]
    public async Task UserWithoutLogsInTheRange_ReturnsNoItemsAndZeroTotal()
    {
        var userId = await SeedUserWithLogsAsync();

        var page = await QueryAsync(userId, FirstDay.AddYears(1), LastDay.AddYears(1), limit: 10, offset: 0);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);
    }

    [Fact]
    public async Task TheTotalIsTheFilteredRangeAndNotTheWholeTable()
    {
        var userId = await SeedUserWithLogsAsync();
        await SeedUserWithLogsAsync();

        var page = await QueryAsync(userId, FirstDay.AddDays(4), FirstDay.AddDays(8), limit: 2, offset: 0);

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(5, page.Total);
        Assert.Equal([FirstDay.AddDays(4), FirstDay.AddDays(5)], page.Items.Select(log => log.LogDate));
    }

    [Fact]
    public async Task ASingleItemPage_ReportsTheTotal()
    {
        var userId = await SeedUserWithLogsAsync();

        var page = await QueryAsync(userId, FirstDay, LastDay, limit: 1, offset: 12);

        Assert.Equal(FirstDay.AddDays(12), Assert.Single(page.Items).LogDate);
        Assert.Equal(SeededDays, page.Total);
    }

    [Fact]
    public async Task OtherUsersLogsNeverAppearNorCount()
    {
        var userId = await SeedUserWithLogsAsync();
        await SeedUserWithLogsAsync();

        var page = await QueryAsync(userId, FirstDay, LastDay, limit: 100, offset: 0);

        Assert.Equal(SeededDays, page.Items.Count);
        Assert.Equal(SeededDays, page.Total);
        Assert.All(page.Items, log => Assert.Equal(userId, log.UserId));
    }

    // Cada fila de la página sale con TODOS sus campos iguales a los que materializa EF.
    [Fact]
    public async Task EveryFieldOfAPageRow_MatchesTheEntityEfMaterializes()
    {
        var userId = await SeedUserWithLogsAsync(rich: true);
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repository = new DailyLogRepository(dbContext, TimeProvider.System);

        var page = await repository.FindByUserAndRangeAsync(userId, FirstDay, LastDay, 100, 0);

        foreach (var fromPage in page.Items)
        {
            var fromEf = await repository.FindByUserAndDateAsync(userId, fromPage.LogDate);
            Assert.Equivalent(fromEf, fromPage, strict: true);
        }

        var rich = page.Items.First(log => log.ActivityType is not null);
        Assert.Equal([SleepDisturbance.Insomnia, SleepDisturbance.EarlyWaking], rich.SleepDisturbances);
        Assert.Equal([new Symptom(SymptomType.Fatigue, 2), new Symptom(SymptomType.Panic, 4)], rich.Symptoms);
        Assert.Equal("nota", rich.Notes);
    }

    [Fact]
    public async Task APageWithItems_IsOneSqlRoundTrip()
    {
        var userId = await SeedUserWithLogsAsync();

        Assert.Equal(1, await CountDailyLogsCommandsAsync(userId, limit: 10, offset: 0));
        Assert.Equal(1, await CountDailyLogsCommandsAsync(userId, limit: 10, offset: 20));
    }

    [Fact]
    public async Task AnEmptyPageBeyondTheRange_NeedsTheFallbackCountAsASecondRoundTrip()
    {
        var userId = await SeedUserWithLogsAsync();

        Assert.Equal(2, await CountDailyLogsCommandsAsync(userId, limit: 10, offset: 30));
    }

    private async Task<int> CountDailyLogsCommandsAsync(Guid userId, int limit, int offset)
    {
        var commands = 0;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Npgsql",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                var statement = activity.GetTagItem("db.statement") as string ?? activity.GetTagItem("db.query.text") as string;
                if (statement is not null && statement.Contains("daily_logs", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref commands);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        await QueryAsync(userId, FirstDay, LastDay, limit, offset);

        return commands;
    }

    private async Task<PagedResult<DailyLog>> QueryAsync(Guid userId, DateOnly from, DateOnly to, int limit, int offset)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await new DailyLogRepository(dbContext, TimeProvider.System)
            .FindByUserAndRangeAsync(userId, from, to, limit, offset);
    }

    private async Task<Guid> SeedUserWithLogsAsync(bool rich = false)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await users.UpsertByGoogleIdAsync(
            new GoogleProfile($"page-{Guid.NewGuid():N}", $"page-{Guid.NewGuid():N}@example.com", "Paging Test", null));
        var logs = new DailyLogRepository(dbContext, TimeProvider.System);

        for (var day = 0; day < SeededDays; day++)
        {
            var log = new DailyLog
            {
                UserId = user.Id,
                LogDate = FirstDay.AddDays(day),
                MoodRating = (short)(1 + (day % 5)),
                AnxietyLevel = 5,
                StressLevel = 5,
                SleepHours = 7.5m,
                SleepQuality = 4,
                SocialFrequency = SocialFrequency.Occasional,
            };

            if (rich && day % 2 == 0)
            {
                log.SleepDisturbances = [SleepDisturbance.Insomnia, SleepDisturbance.EarlyWaking];
                log.ActivityType = ActivityType.Walking;
                log.ActivityMinutes = 30;
                log.Symptoms = [new Symptom(SymptomType.Fatigue, 2), new Symptom(SymptomType.Panic, 4)];
                log.Notes = "nota";
            }

            await logs.UpsertAsync(log);
        }

        return user.Id;
    }
}
