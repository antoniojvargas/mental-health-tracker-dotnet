using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using MentalHealthTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Api.Modules.DailyLog;
using MentalHealthTracker.Api.Modules.DailyLog.Dtos;

namespace MentalHealthTracker.IntegrationTests;

// Cabeceras que añade el pipeline a TODA respuesta, la haya producido un controlador, un
// middleware, el fallback de rutas o el manejador global de excepciones. Se afirma sobre
// respuestas de éxito y de error porque el manejador de excepciones reinicia la respuesta,
// y un error que saliera sin cabeceras sería justo el caso que nadie mira.
[Collection(IntegrationTestGroup.Name)]
public sealed partial class PipelineHeadersTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory), IClassFixture<CustomWebApplicationFactory>
{
    private const string StrictCsp =
        "default-src 'self'; object-src 'none'; frame-ancestors 'self'; base-uri 'self'; form-action 'self'";

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex ThirtyTwoHex();

    public static TheoryData<string> Scenarios => new()
    {
        "ok-health",
        "401-me-without-cookie",
        "401-me-deleted-user",
        "404-api-fallback",
        "404-today",
        "400-validation",
        "500-unexpected",
    };

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task EveryResponse_CarriesTheSecurityHeadersAndAStrictCsp(string scenario)
    {
        using var response = await ProduceAsync(scenario, requestId: null);

        Assert.Equal("nosniff", Single(response, "X-Content-Type-Options"));
        Assert.Equal("SAMEORIGIN", Single(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", Single(response, "Referrer-Policy"));
        Assert.Equal(StrictCsp, Single(response, "Content-Security-Policy"));
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task EveryResponse_GeneratesA32HexRequestIdWhenNoneArrives(string scenario)
    {
        using var response = await ProduceAsync(scenario, requestId: null);

        Assert.Matches(ThirtyTwoHex(), Single(response, "X-Request-Id"));
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task EveryResponse_ReturnsTheIncomingRequestIdUnchanged(string scenario)
    {
        using var response = await ProduceAsync(scenario, requestId: "trace-me-123");

        Assert.Equal("trace-me-123", Single(response, "X-Request-Id"));
    }

    [Fact]
    public async Task GeneratedRequestIds_DifferBetweenRequests()
    {
        var client = Factory.CreateClient();

        var first = Single(await client.GetAsync("/api/health"), "X-Request-Id");
        var second = Single(await client.GetAsync("/api/health"), "X-Request-Id");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task BlankIncomingRequestId_IsReplacedByAGeneratedOne()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.TryAddWithoutValidation("X-Request-Id", "   ");

        using var response = await Factory.CreateClient().SendAsync(request);

        Assert.Matches(ThirtyTwoHex(), Single(response, "X-Request-Id"));
    }

    private async Task<HttpResponseMessage> ProduceAsync(string scenario, string? requestId)
    {
        switch (scenario)
        {
            case "ok-health":
                return await SendAsync(Factory, HttpMethod.Get, "/api/health", null, null, requestId);
            case "401-me-without-cookie":
                return await SendAsync(Factory, HttpMethod.Get, "/api/auth/me", null, null, requestId);
            case "404-api-fallback":
                return await SendAsync(Factory, HttpMethod.Get, "/api/does-not-exist", null, null, requestId);
            case "401-me-deleted-user":
                {
                    var (_, _, token) = await CreateAuthenticatedSessionAsync(NewEmail());
                    await using (var scope = Factory.Services.CreateAsyncScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE users, daily_logs CASCADE");
                    }

                    return await SendAsync(Factory, HttpMethod.Get, "/api/auth/me", token, null, requestId);
                }

            case "404-today":
                {
                    var (_, _, token) = await CreateAuthenticatedSessionAsync(NewEmail());
                    return await SendAsync(Factory, HttpMethod.Get, "/api/logs/today", token, null, requestId);
                }

            case "400-validation":
                {
                    var (_, _, token) = await CreateAuthenticatedSessionAsync(NewEmail());
                    var body = JsonContent.Create(new
                    {
                        logDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        moodRating = 9,
                        anxietyLevel = 5,
                        stressLevel = 5,
                        sleepHours = 7,
                        sleepQuality = 4,
                        sleepDisturbances = Array.Empty<string>(),
                        socialFrequency = "none",
                        symptoms = Array.Empty<object>(),
                    });
                    return await SendAsync(Factory, HttpMethod.Post, "/api/logs", token, body, requestId);
                }

            case "500-unexpected":
                {
                    await using var failing = Factory.WithWebHostBuilder(builder =>
                        builder.ConfigureTestServices(services =>
                            services.AddScoped<IDailyLogService, ThrowingDailyLogService>()));
                    var (_, _, token) = await CreateAuthenticatedSessionAsync(NewEmail());
                    return await SendAsync(failing, HttpMethod.Get, "/api/logs", token, null, requestId);
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }
    }

    private static async Task<HttpResponseMessage> SendAsync(
        WebApplicationFactory<Program> factory,
        HttpMethod method,
        string path,
        string? token,
        HttpContent? content,
        string? requestId)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        if (token is not null)
        {
            request.Headers.TryAddWithoutValidation("Cookie", $"{JwtService.SessionCookieName}={token}");
        }

        if (requestId is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Request-Id", requestId);
        }

        return await factory.CreateClient().SendAsync(request);
    }

    private static string NewEmail() => $"hdr-{Guid.NewGuid():N}@example.com";

    private static string Single(HttpResponseMessage response, string name)
    {
        Assert.True(response.Headers.TryGetValues(name, out var values), $"Falta la cabecera {name}");
        return Assert.Single(values!);
    }

    private sealed class ThrowingDailyLogService : IDailyLogService
    {
        public Task<(DailyLogResponse Response, bool Created)> UpsertAsync(
            Guid userId, CreateDailyLogRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");

        public Task<DailyLogListResponse> ListAsync(
            Guid userId, ListDailyLogsQuery query, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");

        public Task<DailyLogResponse?> GetTodayAsync(
            Guid userId, DateOnly? logDate, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");
    }
}
