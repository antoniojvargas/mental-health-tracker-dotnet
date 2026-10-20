using MentalHealthTracker.Domain.Entities;
using MentalHealthTracker.Domain.Enums;
using MentalHealthTracker.Domain.Models;
using MentalHealthTracker.Domain.ValueObjects;
using MentalHealthTracker.Infrastructure.Persistence;
using MentalHealthTracker.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealthTracker.IntegrationTests;

// Las lecturas de los repositorios son de solo lectura (los llamadores nunca modifican lo que
// reciben), así que no deben dejar entidades rastreadas en el contexto. Las escrituras sí
// necesitan el seguimiento.
[Collection(IntegrationTestGroup.Name)]
public sealed class ChangeTrackingTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory), IClassFixture<CustomWebApplicationFactory>
{
    private static readonly DateOnly LogDate = new(2030, 6, 15);

    [Fact]
    public async Task UpsertByGoogleId_TracksTheUserItSaves()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = new UserRepository(dbContext, TimeProvider.System);

        var user = await users.UpsertByGoogleIdAsync(Profile("track"));

        Assert.Same(user, Assert.Single(dbContext.ChangeTracker.Entries<User>()).Entity);
    }

    [Fact]
    public async Task UpsertByGoogleId_UpdatesAnExistingUserThroughTracking()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = new UserRepository(dbContext, TimeProvider.System);
        var googleId = $"upd-{Guid.NewGuid():N}";
        await users.UpsertByGoogleIdAsync(new GoogleProfile(googleId, "old@example.com", "Old Name", null));

        await using var second = Factory.Services.CreateAsyncScope();
        var secondContext = second.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await new UserRepository(secondContext, TimeProvider.System)
            .UpsertByGoogleIdAsync(new GoogleProfile(googleId, "new@example.com", "New Name", "http://x/a.png"));

        var stored = await secondContext.Users.AsNoTracking().SingleAsync(user => user.GoogleId == googleId);
        Assert.Equal("new@example.com", stored.Email);
        Assert.Equal("New Name", stored.Name);
        Assert.Equal(updated.Id, stored.Id);
    }

    [Fact]
    public async Task FindById_DoesNotTrackTheUser()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await SeedUserAsync();
        var users = new UserRepository(dbContext, TimeProvider.System);

        var found = await users.FindByIdAsync(userId);

        Assert.NotNull(found);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task FindByUserAndDate_DoesNotTrackTheLog()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await SeedUserAsync();
        await SeedLogAsync(userId);
        var logs = new DailyLogRepository(dbContext, TimeProvider.System);

        var found = await logs.FindByUserAndDateAsync(userId, LogDate);

        Assert.NotNull(found);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task FindByUserAndRange_DoesNotTrackTheLogs()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await SeedUserAsync();
        await SeedLogAsync(userId);
        var logs = new DailyLogRepository(dbContext, TimeProvider.System);

        var result = await logs.FindByUserAndRangeAsync(userId, LogDate, LogDate, 10, 0);

        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task FindByUserAndDate_ReturnsTheStoredValuesWithoutTracking()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await SeedUserAsync();
        await SeedLogAsync(userId);

        var found = await new DailyLogRepository(dbContext, TimeProvider.System).FindByUserAndDateAsync(userId, LogDate);

        Assert.Equal(3, found!.MoodRating);
        Assert.Equal(SocialFrequency.Occasional, found.SocialFrequency);
        Assert.Equal([new Symptom(SymptomType.Fatigue, 2)], found.Symptoms);
    }

    private static GoogleProfile Profile(string prefix) =>
        new($"{prefix}-{Guid.NewGuid():N}", $"{prefix}-{Guid.NewGuid():N}@example.com", "Tracking Test", null);

    private async Task<Guid> SeedUserAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return (await new UserRepository(dbContext, TimeProvider.System).UpsertByGoogleIdAsync(Profile("seed"))).Id;
    }

    private async Task SeedLogAsync(Guid userId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await new DailyLogRepository(dbContext, TimeProvider.System).UpsertAsync(new DailyLog
        {
            UserId = userId,
            LogDate = LogDate,
            MoodRating = 3,
            AnxietyLevel = 5,
            StressLevel = 5,
            SleepHours = 7m,
            SleepQuality = 4,
            SocialFrequency = SocialFrequency.Occasional,
            Symptoms = [new Symptom(SymptomType.Fatigue, 2)],
        });
    }
}
