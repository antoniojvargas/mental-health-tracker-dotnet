using MentalHealthTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealthTracker.IntegrationTests;

// El esquema real (tras aplicar todas las migraciones) de daily_logs: solo la clave primaria y
// el índice único compuesto. El compuesto empieza por user_id, así que cubre por prefijo las
// búsquedas por usuario y la FK; un índice aparte solo sobre user_id sería redundante.
[Collection(IntegrationTestGroup.Name)]
public sealed class DailyLogsIndexTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory), IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task DailyLogs_HasOnlyThePrimaryKeyAndTheUniqueCompositeIndex()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var indexes = await dbContext.Database
            .SqlQuery<string>($"SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'daily_logs'")
            .ToListAsync();

        Assert.Equal(
            ["IX_daily_logs_user_id_log_date", "PK_daily_logs"],
            indexes.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task TheCompositeIndexIsUniqueAndStartsWithUserId()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var definition = await dbContext.Database
            .SqlQuery<string>($"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE indexname = 'IX_daily_logs_user_id_log_date'")
            .SingleAsync();

        Assert.Contains("UNIQUE", definition, StringComparison.Ordinal);
        Assert.Contains("(user_id, log_date)", definition, StringComparison.Ordinal);
    }
}
