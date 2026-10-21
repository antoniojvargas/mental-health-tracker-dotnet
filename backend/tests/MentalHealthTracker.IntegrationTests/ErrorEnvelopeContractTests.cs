using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MentalHealthTracker.Api.Modules.DailyLog;
using MentalHealthTracker.Api.Modules.DailyLog.Dtos;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MentalHealthTracker.Infrastructure.Persistence;

namespace MentalHealthTracker.IntegrationTests;

// Contrato único de errores: toda respuesta de error de la API, venga de donde venga
// (excepción de dominio, middleware, rate limiter, fallback de rutas o fallo inesperado),
// tiene el cuerpo {"error":{"code","message","details":[...]}} con content-type JSON.
// Cada caso fabrica una respuesta de error real; la forma se afirma en un único sitio
// para que un nuevo camino de error que se salga del sobre rompa esta clase.
[Collection(IntegrationTestGroup.Name)]
public sealed class ErrorEnvelopeContractTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Theory]
    [InlineData("me-deleted-user", HttpStatusCode.Unauthorized, "UNAUTHORIZED")]
    [InlineData("require-auth", HttpStatusCode.Unauthorized, "UNAUTHORIZED")]
    [InlineData("today-not-found", HttpStatusCode.NotFound, "NOT_FOUND")]
    [InlineData("unknown-route", HttpStatusCode.NotFound, "NOT_FOUND")]
    [InlineData("validation", HttpStatusCode.BadRequest, "VALIDATION_ERROR")]
    [InlineData("rate-limited", HttpStatusCode.TooManyRequests, "RATE_LIMITED")]
    [InlineData("unexpected", HttpStatusCode.InternalServerError, "INTERNAL_ERROR")]
    public async Task ErrorResponse_HasTheSameEnvelope(string scenario, HttpStatusCode status, string code)
    {
        using var response = await ProduceErrorAsync(scenario);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        // Un único campo en la raíz: nada de ProblemDetails (type/title/status) mezclado.
        Assert.Equal(["error"], root.EnumerateObject().Select(p => p.Name));

        var error = root.GetProperty("error");
        Assert.Equal(
            ["code", "details", "message"],
            error.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("message").GetString()));

        var details = error.GetProperty("details");
        Assert.Equal(JsonValueKind.Array, details.ValueKind);
        foreach (var detail in details.EnumerateArray())
        {
            Assert.False(string.IsNullOrWhiteSpace(detail.GetProperty("field").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(detail.GetProperty("message").GetString()));
        }

        if (scenario == "validation")
        {
            Assert.NotEmpty(details.EnumerateArray());
        }
    }

    private Task<HttpResponseMessage> ProduceErrorAsync(string scenario) => scenario switch
    {
        "me-deleted-user" => MeWithDeletedUserAsync(),
        "require-auth" => Factory.CreateClient().GetAsync("/api/logs"),
        "today-not-found" => TodayWithoutLogAsync(),
        "unknown-route" => Factory.CreateClient().GetAsync("/api/does-not-exist"),
        "validation" => InvalidPostAsync(),
        "rate-limited" => RateLimitedAsync(),
        "unexpected" => UnexpectedFailureAsync(),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
    };

    private async Task<HttpResponseMessage> MeWithDeletedUserAsync()
    {
        var (client, _, _) = await CreateAuthenticatedSessionAsync($"contract-{Guid.NewGuid():N}@example.com");
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE users, daily_logs CASCADE");
        }

        return await client.GetAsync("/api/auth/me");
    }

    private async Task<HttpResponseMessage> TodayWithoutLogAsync()
    {
        var (client, _, _) = await CreateAuthenticatedSessionAsync($"contract-{Guid.NewGuid():N}@example.com");
        return await client.GetAsync("/api/logs/today");
    }

    private async Task<HttpResponseMessage> InvalidPostAsync()
    {
        var (client, _, _) = await CreateAuthenticatedSessionAsync($"contract-{Guid.NewGuid():N}@example.com");
        return await client.PostAsJsonAsync(
            "/api/logs",
            new CreateDailyLogRequestBuilder().WithMoodRating(9).Build(),
            AppJsonOptions);
    }

    // El limitador es estado del host: un host nuevo (WithWebHostBuilder) evita agotar
    // la cuota del que comparten el resto de casos.
    private async Task<HttpResponseMessage> RateLimitedAsync()
    {
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

        return last;
    }

    private async Task<HttpResponseMessage> UnexpectedFailureAsync()
    {
        await using var failing = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped<IDailyLogService, ThrowingDailyLogService>()));

        var (_, _, token) = await CreateAuthenticatedSessionAsync($"contract-{Guid.NewGuid():N}@example.com");
        return await failing.CreateClient().SendAuthenticatedAsync(HttpMethod.Get, "/api/logs", token);
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
