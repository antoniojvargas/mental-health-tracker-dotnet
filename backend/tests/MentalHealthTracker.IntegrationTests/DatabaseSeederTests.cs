using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using MentalHealthTracker.Domain.Common;
using MentalHealthTracker.Domain.Enums;
using MentalHealthTracker.Infrastructure.Persistence;
using MentalHealthTracker.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MentalHealthTracker.IntegrationTests;

// `dotnet run --seed` carga un usuario demo con 60 días de registros pseudoaleatorios pero
// deterministas (Random(42)). Con un reloj fijo, el resultado es EXACTAMENTE el de
// Contracts/seed-demo-logs.json; si cambia algo del generador o del orden de las llamadas a
// Random, este test lo dice. Para regenerar el golden tras un cambio intencionado:
//   UPDATE_SEED_GOLDEN=1 dotnet test --filter DatabaseSeederTests
[Collection(IntegrationTestGroup.Name)]
public sealed class DatabaseSeederTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory), IClassFixture<CustomWebApplicationFactory>
{
    private static readonly DateTimeOffset FixedNow = new(2030, 6, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Seed_WithAFixedClock_GeneratesExactlyTheGoldenSixtyLogs()
    {
        var actual = await SeedAndDumpAsync();

        var goldenPath = Path.Combine(AppContext.BaseDirectory, "Contracts", "seed-demo-logs.json");
        if (Environment.GetEnvironmentVariable("UPDATE_SEED_GOLDEN") == "1")
        {
            var sourcePath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "Contracts", "seed-demo-logs.json"));
            await File.WriteAllTextAsync(sourcePath, actual.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
            return;
        }

        var golden = JsonNode.Parse(await File.ReadAllTextAsync(goldenPath))!;
        Assert.Equal(60, actual.AsArray().Count);
        Assert.True(
            JsonNode.DeepEquals(golden, actual),
            $"Los registros sembrados difieren de {goldenPath}.");
    }

    [Fact]
    public async Task Seed_RunTwice_IsIdempotent()
    {
        var first = await SeedAndDumpAsync();
        var second = await SeedAndDumpAsync();

        Assert.Equal(60, second.AsArray().Count);
        Assert.True(JsonNode.DeepEquals(first, second));
    }

    private async Task<JsonNode> SeedAndDumpAsync()
    {
        var clock = new FixedClock(FixedNow);
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var seeder = new DatabaseSeeder(
            new UserRepository(dbContext, clock),
            new DailyLogRepository(dbContext, clock),
            clock,
            NullLogger<DatabaseSeeder>.Instance);
        await seeder.SeedAsync();

        var logs = await dbContext.DailyLogs.AsNoTracking().OrderBy(log => log.LogDate).ToListAsync();
        var array = new JsonArray();
        foreach (var log in logs)
        {
            array.Add(new JsonObject
            {
                ["logDate"] = log.LogDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["moodRating"] = log.MoodRating,
                ["anxietyLevel"] = log.AnxietyLevel,
                ["stressLevel"] = log.StressLevel,
                ["sleepHours"] = log.SleepHours,
                ["sleepQuality"] = log.SleepQuality,
                ["sleepDisturbances"] = new JsonArray(log.SleepDisturbances
                    .Select(item => (JsonNode)SnakeCaseEnum<SleepDisturbance>.ToSnakeCase(item)).ToArray()),
                ["activityType"] = log.ActivityType is null ? null : SnakeCaseEnum<ActivityType>.ToSnakeCase(log.ActivityType.Value),
                ["activityMinutes"] = log.ActivityMinutes,
                ["socialFrequency"] = SnakeCaseEnum<SocialFrequency>.ToSnakeCase(log.SocialFrequency),
                ["symptoms"] = new JsonArray(log.Symptoms
                    .Select(symptom => (JsonNode)new JsonObject
                    {
                        ["type"] = SnakeCaseEnum<SymptomType>.ToSnakeCase(symptom.Type),
                        ["severity"] = symptom.Severity,
                    }).ToArray()),
                ["notes"] = log.Notes,
                ["createdAt"] = log.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
                ["updatedAt"] = log.UpdatedAt.ToString("O", CultureInfo.InvariantCulture),
            });
        }

        return array;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
