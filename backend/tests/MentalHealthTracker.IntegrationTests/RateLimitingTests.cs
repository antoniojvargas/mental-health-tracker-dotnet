using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Domain.Models;
using MentalHealthTracker.Domain.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealthTracker.IntegrationTests;

// Verifica las políticas de rate limiting definidas en Program.cs: "auth" (20 req/15 min
// por IP) sobre el callback de Google OAuth, y "write" (30 req/15 min por id de usuario)
// sobre POST /api/logs. Los límites (20 y 30) son el contrato vigente en Program.cs.
// Cada clase de test tiene su propia instancia de ApiWebApplicationFactory, por lo que el
// estado del rate limiter (y agotar cuotas) queda aislado a esta clase.
public sealed class RateLimitingTests : IClassFixture<ApiWebApplicationFactory>
{
    private const int AuthPermitLimit = 20;
    private const int WritePermitLimit = 30;

    private const string AuthCallbackUrl = "/api/auth/google/callback?state=x&code=y";

    private readonly ApiWebApplicationFactory _factory;

    public RateLimitingTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthPolicy_WhenLimitExceeded_Returns429WithErrorBodyAndRateLimitHeaders()
    {
        // El callback responde 302 hacia el frontend externo; el RedirectHandler del
        // WebApplicationFactory lo seguiría dentro del TestServer (404), por eso se
        // desactiva para observar el status real del endpoint.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        for (var i = 0; i < AuthPermitLimit; i++)
        {
            var response = await client.GetAsync(AuthCallbackUrl);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var rejected = await client.GetAsync(AuthCallbackUrl);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("20", SingleHeader(rejected, "RateLimit-Limit"));
        Assert.Equal("0", SingleHeader(rejected, "RateLimit-Remaining"));
        Assert.NotNull(SingleHeader(rejected, "RateLimit-Reset"));
        Assert.NotNull(SingleHeader(rejected, "Retry-After"));

        using var document = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        var error = document.RootElement.GetProperty("error");
        Assert.Equal("RATE_LIMITED", error.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(error.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task WritePolicy_ExhaustingOneUserQuota_DoesNotAffectAnotherUser()
    {
        var (_, tokenA) = await CreateAuthenticatedSessionAsync();
        var (_, tokenB) = await CreateAuthenticatedSessionAsync();

        for (var i = 0; i < WritePermitLimit; i++)
        {
            var response = await PostLogAsync(tokenA, Body());
            Assert.True(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
                $"El usuario A debería escribir dentro de su cuota, recibió {response.StatusCode}");
        }

        var rejectedA = await PostLogAsync(tokenA, Body());
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedA.StatusCode);
        Assert.Equal("30", SingleHeader(rejectedA, "RateLimit-Limit"));
        Assert.Equal("0", SingleHeader(rejectedA, "RateLimit-Remaining"));

        using var document = JsonDocument.Parse(await rejectedA.Content.ReadAsStringAsync());
        Assert.Equal("RATE_LIMITED", document.RootElement.GetProperty("error").GetProperty("code").GetString());

        var responseB = await PostLogAsync(tokenB, Body());
        Assert.Equal(HttpStatusCode.Created, responseB.StatusCode);
    }

    private static string SingleHeader(HttpResponseMessage response, string name)
    {
        Assert.True(response.Headers.TryGetValues(name, out var values), $"Falta la cabecera {name}");
        return Assert.Single(values!);
    }

    private async Task<HttpResponseMessage> PostLogAsync(string token, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/logs");
        request.Headers.TryAddWithoutValidation("Cookie", $"{JwtService.SessionCookieName}={token}");
        request.Content = JsonContent.Create(body);

        return await _factory.CreateClient().SendAsync(request);
    }

    private async Task<(Domain.Entities.User User, string Token)> CreateAuthenticatedSessionAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var jwtService = scope.ServiceProvider.GetRequiredService<JwtService>();

        var email = $"e2e-{Guid.NewGuid():N}@example.com";
        var user = await repository.UpsertByGoogleIdAsync(
            new GoogleProfile($"e2e-{Guid.NewGuid():N}", email, "Integration Test User", null));

        return (user, jwtService.Sign(user.Id, user.Email));
    }

    private static object Body() => new
    {
        logDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        moodRating = 3,
        anxietyLevel = 5,
        stressLevel = 5,
        sleepHours = 7.5,
        sleepQuality = 4,
        sleepDisturbances = new[] { "none" },
        activityType = "walking",
        activityMinutes = 30,
        socialFrequency = "occasional",
        symptoms = new[] { new { type = "fatigue", severity = 3 } },
        notes = (string?)null,
    };
}