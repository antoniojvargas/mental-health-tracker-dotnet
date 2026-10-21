using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Domain.Entities;
using MentalHealthTracker.Domain.Enums;
using MentalHealthTracker.Infrastructure.Persistence;
using MentalHealthTracker.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealthTracker.IntegrationTests;

// Benchmark simple de GET /api/logs (1000 peticiones, en secuencia y con 8 en paralelo) sobre la
// API completa en proceso. Solo se ejecuta con RUN_BENCHMARK=1 (siempre sale "omitido" en la
// suite normal); BENCHMARK_LABEL etiqueta la variante y BENCHMARK_OUTPUT es el fichero al que se
// añade una línea JSON por ejecución. Conviene lanzarlo en Release:
//   RUN_BENCHMARK=1 BENCHMARK_LABEL=x BENCHMARK_OUTPUT=/tmp/b.jsonl \
//     dotnet test -c Release --filter LogsListingBenchmark
[Collection(IntegrationTestGroup.Name)]
public sealed class LogsListingBenchmark(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const int Warmup = 200;

    private const int Requests = 1000;

    private const int Workers = 8;

    [BenchmarkFact]
    public async Task Get_Logs_1000_Requests()
    {
        var (_, user, token) = await CreateAuthenticatedSessionAsync($"bench-{Guid.NewGuid():N}@example.com");
        await SeedThirtyDaysAsync(user.Id);
        using var client = Factory.CreateClient();
        client.DefaultRequestHeaders.AddSessionCookie(token);

        // Comprobación de que lo que se mide devuelve datos: 30 registros.
        using (var probe = await client.GetAsync("/api/logs"))
        {
            Assert.Equal(HttpStatusCode.OK, probe.StatusCode);
            using var body = JsonDocument.Parse(await probe.Content.ReadAsStringAsync());
            Assert.Equal(30, body.RootElement.GetProperty("data").GetArrayLength());
        }

        for (var i = 0; i < Warmup; i++)
        {
            await GetAsync(client);
        }

        var sequential = new double[Requests];
        var sequentialWatch = Stopwatch.StartNew();
        for (var i = 0; i < Requests; i++)
        {
            sequential[i] = await TimedGetAsync(client);
        }

        sequentialWatch.Stop();

        var parallel = new double[Requests];
        var parallelWatch = Stopwatch.StartNew();
        await Task.WhenAll(Enumerable.Range(0, Workers).Select(worker => Task.Run(async () =>
        {
            for (var i = worker; i < Requests; i += Workers)
            {
                parallel[i] = await TimedGetAsync(client);
            }
        })));
        parallelWatch.Stop();

        var result = new
        {
            label = Environment.GetEnvironmentVariable("BENCHMARK_LABEL") ?? "unlabeled",
            sequential = Summarize(sequential, sequentialWatch.Elapsed.TotalSeconds),
            parallel = Summarize(parallel, parallelWatch.Elapsed.TotalSeconds),
        };
        var line = JsonSerializer.Serialize(result);
        var output = Environment.GetEnvironmentVariable("BENCHMARK_OUTPUT");
        if (!string.IsNullOrEmpty(output))
        {
            await File.AppendAllTextAsync(output, line + Environment.NewLine);
        }
    }

    private static async Task<double> TimedGetAsync(HttpClient client)
    {
        var start = Stopwatch.GetTimestamp();
        await GetAsync(client);
        return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    private static async Task GetAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/logs");
        response.EnsureSuccessStatusCode();
        _ = await response.Content.ReadAsStringAsync();
    }

    private static object Summarize(double[] latenciesMs, double totalSeconds)
    {
        var sorted = latenciesMs.Order().ToArray();
        double Percentile(double p) => sorted[(int)Math.Min(sorted.Length - 1, Math.Floor(sorted.Length * p))];

        return new
        {
            requestsPerSecond = Math.Round(latenciesMs.Length / totalSeconds, 1),
            meanMs = Math.Round(latenciesMs.Average(), 3),
            p50Ms = Math.Round(Percentile(0.50), 3),
            p95Ms = Math.Round(Percentile(0.95), 3),
            p99Ms = Math.Round(Percentile(0.99), 3),
        };
    }

    private async Task SeedThirtyDaysAsync(Guid userId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logs = new DailyLogRepository(dbContext, TimeProvider.System);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        for (var day = 0; day < 30; day++)
        {
            await logs.UpsertAsync(new DailyLog
            {
                UserId = userId,
                LogDate = today.AddDays(-day),
                MoodRating = (short)(1 + (day % 5)),
                AnxietyLevel = 5,
                StressLevel = 5,
                SleepHours = 7.5m,
                SleepQuality = 4,
                SleepDisturbances = [SleepDisturbance.Insomnia],
                ActivityType = ActivityType.Walking,
                ActivityMinutes = 30,
                SocialFrequency = SocialFrequency.Occasional,
                Symptoms = [new MentalHealthTracker.Domain.ValueObjects.Symptom(SymptomType.Fatigue, 2)],
                Notes = "benchmark",
            });
        }
    }
}

public sealed class BenchmarkFactAttribute : FactAttribute
{
    public BenchmarkFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_BENCHMARK") != "1")
        {
            Skip = "Benchmark: ejecútalo con RUN_BENCHMARK=1.";
        }
    }
}
