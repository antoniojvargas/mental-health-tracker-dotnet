using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Api.Modules.DailyLog.Dtos;
using MentalHealthTracker.Domain.Enums;
using MentalHealthTracker.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealthTracker.IntegrationTests;

// Contrato HTTP congelado en fixtures versionados (Contracts/*.json): lo que el cliente
// recibe, nombre a nombre. Si un cambio altera una forma (un campo renombrado, un enum que
// deja de ser snake_case, sleepHours que pasa a string, un error sin sobre), el golden se
// rompe. Los valores que cambian en cada ejecución se normalizan antes de comparar:
//   * `id` -> "<guid>" solo si de verdad es un Guid;
//   * `createdAt`/`updatedAt` -> "<o-datetime>" solo si cumplen el formato "O" con
//     7 decimales y offset (2026-09-13T17:18:21.1234567+00:00). Si el formato cambiara, el
//     valor no se normaliza y la comparación falla.
// Para regenerar un fixture tras un cambio de contrato INTENCIONADO:
//   UPDATE_GOLDEN=1 dotnet test --filter ContractGoldenTests
// y revisar el diff de Contracts/ antes de hacer commit.
[Collection(IntegrationTestGroup.Name)]
public sealed partial class ContractGoldenTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const string LogDate = "2026-03-15";

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}\+00:00$")]
    private static partial Regex RoundTripDate();

    private static readonly CreateDailyLogRequest FixedBody = new CreateDailyLogRequestBuilder()
        .WithLogDate(DateOnly.ParseExact(LogDate, "yyyy-MM-dd", CultureInfo.InvariantCulture))
        .WithMoodRating(4)
        .WithAnxietyLevel(6)
        .WithStressLevel(3)
        .WithSleepHours(7.5m)
        .WithSleepQuality(4)
        .WithSleepDisturbances([SleepDisturbance.FrequentWaking, SleepDisturbance.EarlyWaking])
        .WithActivityType(ActivityType.Walking)
        .WithActivityMinutes(30)
        .WithSocialFrequency(SocialFrequency.Occasional)
        .WithSymptoms([new Symptom(SymptomType.AppetiteChange, 3), new Symptom(SymptomType.LowMood, 2)])
        .WithNotes("fixed body")
        .Build();

    [Fact]
    public async Task PostLogs_Created_MatchesTheGolden()
    {
        var (_, _, token) = await CreateAuthenticatedSessionAsync(NewEmail());

        using var response = await SendAsync(Factory, HttpMethod.Post, "/api/logs", token, JsonBody(FixedBody));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await AssertGoldenAsync("post-logs-created", response);
    }

    [Fact]
    public async Task PostLogs_Updated_HasTheSameShapeAsCreated()
    {
        var (_, _, token) = await CreateAuthenticatedSessionAsync(NewEmail());
        using var first = await SendAsync(Factory, HttpMethod.Post, "/api/logs", token, JsonBody(FixedBody));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var second = await SendAsync(Factory, HttpMethod.Post, "/api/logs", token, JsonBody(FixedBody));

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        await AssertGoldenAsync("post-logs-created", second);
    }

    [Fact]
    public async Task GetMe_MatchesTheGolden()
    {
        var (_, _, token) = await CreateAuthenticatedSessionAsync(NewEmail("golden-me"), "Ana Gómez");

        using var response = await SendAsync(Factory, HttpMethod.Get, "/api/auth/me", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertGoldenAsync("get-me", response, NormalizeEmail);
    }

    [Fact]
    public async Task GetToday_WithDate_MatchesTheGolden()
    {
        var (_, _, token) = await CreateAuthenticatedSessionAsync(NewEmail());
        using var created = await SendAsync(Factory, HttpMethod.Post, "/api/logs", token, JsonBody(FixedBody));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var response = await SendAsync(Factory, HttpMethod.Get, $"/api/logs/today?date={LogDate}", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertGoldenAsync("get-today", response);
    }

    [Fact]
    public async Task Error400_Validation_MatchesTheGolden()
    {
        var (_, _, token) = await CreateAuthenticatedSessionAsync(NewEmail());
        var invalid = new CreateDailyLogRequestBuilder()
            .WithLogDate(DateOnly.ParseExact(LogDate, "yyyy-MM-dd", CultureInfo.InvariantCulture))
            .WithMoodRating(9)
            .Build();

        using var response = await SendAsync(Factory, HttpMethod.Post, "/api/logs", token, JsonBody(invalid));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertGoldenAsync("error-400-validation", response);
    }

    [Fact]
    public async Task Error401_MeWithoutCookie_MatchesTheGolden()
    {
        using var response = await SendAsync(Factory, HttpMethod.Get, "/api/auth/me", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertGoldenAsync("error-401", response);
    }

    [Fact]
    public async Task Error401_MeWithDeletedUser_HasTheSameBodyAsWithoutCookie()
    {
        var (client, _, _) = await CreateAuthenticatedSessionAsync(NewEmail());
        await TruncateAsync();

        using var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertGoldenAsync("error-401", response);
    }

    [Fact]
    public async Task Error404_TodayWithoutLog_MatchesTheGolden()
    {
        var (_, _, token) = await CreateAuthenticatedSessionAsync(NewEmail());

        using var response = await SendAsync(Factory, HttpMethod.Get, "/api/logs/today?date=2026-01-02", token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertGoldenAsync("error-404-today", response);
    }

    [Fact]
    public async Task Error404_UnknownApiRoute_MatchesTheGolden()
    {
        using var response = await SendAsync(Factory, HttpMethod.Get, "/api/does-not-exist", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertGoldenAsync("error-404-route", response);
    }

    [Fact]
    public async Task Error429_RateLimited_MatchesTheGolden()
    {
        // Host aparte: agotar la cuota no debe afectar al resto de pruebas.
        await using var isolated = Factory.WithWebHostBuilder(_ => { });
        var client = isolated.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        HttpResponseMessage last;
        var attempts = 0;
        do
        {
            last = await client.GetAsync("/api/auth/google/callback?state=x&code=y");
            attempts++;
        }
        while (last.StatusCode != HttpStatusCode.TooManyRequests && attempts < 50);

        Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode);
        await AssertGoldenAsync("error-429", last);
    }

    // --- infraestructura de goldens ---

    private static void NormalizeEmail(JsonObject root)
    {
        // El email lleva un sufijo aleatorio por ejecución; se afirma el prefijo y se
        // sustituye por un valor fijo.
        var email = root["email"]!.GetValue<string>();
        Assert.StartsWith("golden-me-", email);
        Assert.EndsWith("@example.com", email);
        root["email"] = "<email>";
    }

    private static async Task AssertGoldenAsync(
        string name,
        HttpResponseMessage response,
        Action<JsonObject>? extraNormalization = null)
    {
        Assert.StartsWith("application/json", response.Content.Headers.ContentType?.MediaType);
        var actual = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Normalize(actual);
        if (extraNormalization is not null)
        {
            extraNormalization(actual.AsObject());
        }

        var goldenPath = Path.Combine(AppContext.BaseDirectory, "Contracts", $"{name}.json");

        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            var sourcePath = Path.Combine(SourceDirectory(), "Contracts", $"{name}.json");
            await File.WriteAllTextAsync(
                sourcePath,
                Pretty(actual) + "\n");
            return;
        }

        Assert.True(File.Exists(goldenPath), $"Falta el golden {name}.json (regenera con UPDATE_GOLDEN=1).");
        var expected = JsonNode.Parse(await File.ReadAllTextAsync(goldenPath))!;

        Assert.True(
            JsonNode.DeepEquals(expected, actual),
            $"El contrato '{name}' cambió.\nEsperado:\n{Pretty(expected)}\nActual:\n{Pretty(actual)}");
    }

    private static void Normalize(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    if (obj[key] is JsonValue value && value.TryGetValue<string>(out var text))
                    {
                        if (key == "id" && Guid.TryParse(text, out _))
                        {
                            obj[key] = "<guid>";
                        }
                        else if (key is "createdAt" or "updatedAt" && RoundTripDate().IsMatch(text))
                        {
                            obj[key] = "<o-datetime>";
                        }
                    }
                    else
                    {
                        Normalize(obj[key]);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Normalize(item);
                }

                break;
        }
    }

    private static readonly JsonSerializerOptions PrettyOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static string Pretty(JsonNode node) => node.ToJsonString(PrettyOptions);

    private static string SourceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MentalHealthTracker.IntegrationTests.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No se encontró el directorio del proyecto.");
    }

    private async Task TruncateAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MentalHealthTracker.Infrastructure.Persistence.AppDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE users, daily_logs CASCADE");
    }

    private static string NewEmail(string prefix = "golden") => $"{prefix}-{Guid.NewGuid():N}@example.com";

    private static async Task<HttpResponseMessage> SendAsync(
        WebApplicationFactory<Program> factory,
        HttpMethod method,
        string path,
        string? token,
        HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        if (token is not null)
        {
            request.Headers.AddSessionCookie(token);
        }

        return await factory.CreateClient().SendAsync(request);
    }
}
