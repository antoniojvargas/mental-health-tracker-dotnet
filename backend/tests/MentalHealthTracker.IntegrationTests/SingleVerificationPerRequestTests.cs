using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using MentalHealthTracker.Api.Core.Configuration;
using MentalHealthTracker.Api.Modules.Auth;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.IntegrationTests;

// POST /api/logs pasa por dos consumidores de la identidad de la sesión: la partición de la
// política de rate limiting "write" y SessionAuthenticationHandler. SessionTokenReader hace que el
// JWT se verifique una sola vez y el segundo reutilice el resultado.
[Collection(IntegrationTestGroup.Name)]
public sealed class SingleVerificationPerRequestTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory), IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task PostLogs_WithValidSession_VerifiesTheTokenOnce()
    {
        var counter = new VerificationCounter();
        await using var counting = CountingHost(counter);
        var (_, _, token) = await CreateAuthenticatedSessionAsync($"once-{Guid.NewGuid():N}@example.com");

        counter.Reset();
        using var response = await PostLogAsync(counting, token);

        Assert.True(response.IsSuccessStatusCode, $"POST /api/logs devolvió {(int)response.StatusCode}");
        Assert.Equal(1, counter.Calls);
    }

    [Fact]
    public async Task PostLogs_WithInvalidSession_Returns401AfterOneVerification()
    {
        var counter = new VerificationCounter();
        await using var counting = CountingHost(counter);
        var (_, _, token) = await CreateAuthenticatedSessionAsync($"once-{Guid.NewGuid():N}@example.com");

        counter.Reset();
        using var response = await PostLogAsync(counting, TamperSignature(token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, counter.Calls);
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> CountingHost(VerificationCounter counter) =>
        Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<JwtService>(provider => new CountingJwtService(
                    provider.GetRequiredService<IOptions<JwtOptions>>(),
                    provider.GetRequiredService<TimeProvider>(),
                    counter))));

    private static async Task<HttpResponseMessage> PostLogAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host,
        string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/logs")
        {
            Content = JsonContent.Create(ValidLog()),
        };
        request.Headers.TryAddWithoutValidation("Cookie", $"{SessionCookie.AccessTokenName}={token}");

        return await host.CreateClient().SendAsync(request);
    }

    private static object ValidLog() => new
    {
        logDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        moodRating = 5,
        anxietyLevel = 5,
        stressLevel = 5,
        sleepHours = 7,
        sleepQuality = 4,
        sleepDisturbances = Array.Empty<string>(),
        socialFrequency = "none",
        symptoms = Array.Empty<object>(),
    };

    private sealed class VerificationCounter
    {
        private int _calls;

        public int Calls => _calls;

        public void Increment() => Interlocked.Increment(ref _calls);

        public void Reset() => Interlocked.Exchange(ref _calls, 0);
    }

    private sealed class CountingJwtService(
        IOptions<JwtOptions> options,
        TimeProvider timeProvider,
        VerificationCounter counter) : JwtService(options, timeProvider)
    {
        public override (Guid UserId, string Email)? Verify(string? token)
        {
            counter.Increment();
            return base.Verify(token);
        }
    }
}
