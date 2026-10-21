using System.Data;
using MentalHealthTracker.Domain.Entities;
using MentalHealthTracker.Domain.Enums;
using MentalHealthTracker.Domain.Models;
using MentalHealthTracker.Domain.Repositories;
using MentalHealthTracker.Infrastructure.Persistence;
using MentalHealthTracker.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealthTracker.IntegrationTests;

// UpsertAsync usa SQL crudo sobre la conexión del DbContext. Estas pruebas fijan cómo se
// comporta con esa conexión: participa en la transacción del contexto (rollback y commit
// también valen para el upsert) y no la deja abierta si ella fue quien la abrió.
[Collection(IntegrationTestGroup.Name)]
public sealed class DailyLogRepositoryConnectionTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly DateOnly LogDate = new(2030, 6, 15);

    [Fact]
    public async Task Upsert_InsideATransactionThatIsRolledBack_LeavesNoRow()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await CreateUserAsync(scope);
        var repository = new DailyLogRepository(dbContext, TimeProvider.System);

        await using (var transaction = await dbContext.Database.BeginTransactionAsync())
        {
            var result = await repository.UpsertAsync(NewLog(userId));
            Assert.True(result.Created);

            await transaction.RollbackAsync();
        }

        Assert.False(await dbContext.DailyLogs.AnyAsync(log => log.UserId == userId));
    }

    [Fact]
    public async Task Upsert_InsideATransactionThatIsCommitted_PersistsTheRowAndKeepsTheConnectionUsable()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await CreateUserAsync(scope);
        var repository = new DailyLogRepository(dbContext, TimeProvider.System);

        await using (var transaction = await dbContext.Database.BeginTransactionAsync())
        {
            await repository.UpsertAsync(NewLog(userId));

            // La conexión sigue abierta y dentro de la transacción tras el upsert.
            Assert.Equal(ConnectionState.Open, dbContext.Database.GetDbConnection().State);
            Assert.NotNull(dbContext.Database.CurrentTransaction);

            await transaction.CommitAsync();
        }

        Assert.True(await dbContext.DailyLogs.AnyAsync(log => log.UserId == userId));
    }

    [Fact]
    public async Task Upsert_OutsideATransaction_ClosesTheConnectionItOpened()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await CreateUserAsync(scope);
        var repository = new DailyLogRepository(dbContext, TimeProvider.System);
        Assert.Equal(ConnectionState.Closed, dbContext.Database.GetDbConnection().State);

        await repository.UpsertAsync(NewLog(userId));

        Assert.Equal(ConnectionState.Closed, dbContext.Database.GetDbConnection().State);
    }

    [Fact]
    public async Task Upsert_WithAnAlreadyOpenConnection_LeavesItOpen()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await CreateUserAsync(scope);
        var repository = new DailyLogRepository(dbContext, TimeProvider.System);

        await dbContext.Database.OpenConnectionAsync();
        await repository.UpsertAsync(NewLog(userId));

        Assert.Equal(ConnectionState.Open, dbContext.Database.GetDbConnection().State);
        await dbContext.Database.CloseConnectionAsync();
        Assert.Equal(ConnectionState.Closed, dbContext.Database.GetDbConnection().State);
    }

    private static async Task<Guid> CreateUserAsync(AsyncServiceScope scope)
    {
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await users.UpsertByGoogleIdAsync(
            new GoogleProfile($"conn-{Guid.NewGuid():N}", $"conn-{Guid.NewGuid():N}@example.com", "Connection Test", null));

        return user.Id;
    }

    private static DailyLog NewLog(Guid userId) => new()
    {
        UserId = userId,
        LogDate = LogDate,
        MoodRating = 3,
        AnxietyLevel = 5,
        StressLevel = 5,
        SleepHours = 7m,
        SleepQuality = 4,
        SocialFrequency = SocialFrequency.Occasional,
    };
}
