using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using MentalHealthTracker.Api.Core.RateLimiting;
using MentalHealthTracker.Api.Modules.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.IntegrationTests;

// Fija los efectos observables del orden del pipeline (ver ApiPipeline.UseApiPipeline):
// las respuestas que cortocircuitan antes de llegar a un controlador (401 de RequireAuth,
// 429 del limitador) siguen llevando RequestId y cabeceras de seguridad; el limitador de
// escritura corre antes que la autenticación sin que las peticiones anónimas gasten cuota;
// y, detrás del proxy, el limitador ya ve la IP reenviada y no la del proxy.
[Collection(IntegrationTestGroup.Name)]
public sealed class PipelineOrderEffectsTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory), IClassFixture<CustomWebApplicationFactory>
{
    private int WritePermitLimit =>
        Factory.Services.GetRequiredService<IOptions<RateLimitingOptions>>().Value.Write.PermitLimit;

    private const string AuthCallbackUrl = "/api/auth/google/callback?state=x&code=y";

    [Fact]
    public async Task Unauthorized401_CarriesRequestIdAndSecurityHeaders()
    {
        using var response = await Factory.CreateClient().PostAsJsonAsync("/api/logs", ValidLog());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertCarriesPipelineHeaders(response);
    }

    [Fact]
    public async Task RateLimited429_CarriesRequestIdAndSecurityHeaders()
    {
        await using var isolated = Factory.WithWebHostBuilder(_ => { });
        var client = isolated.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await SendUntilAsync(client, HttpStatusCode.TooManyRequests, () =>
            new HttpRequestMessage(HttpMethod.Get, AuthCallbackUrl));

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        AssertCarriesPipelineHeaders(response);
    }

    [Fact]
    public async Task AnonymousPostToLogs_Returns401AndDoesNotConsumeTheWriteQuota()
    {
        var (_, _, token) = await CreateAuthenticatedSessionAsync($"order-{Guid.NewGuid():N}@example.com");
        await using var isolated = Factory.WithWebHostBuilder(_ => { });
        var client = isolated.CreateClient();

        for (var i = 0; i < WritePermitLimit + 10; i++)
        {
            using var anonymous = await client.PostAsJsonAsync("/api/logs", ValidLog());
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }

        for (var i = 0; i < WritePermitLimit; i++)
        {
            using var request = AuthenticatedLogRequest(token);
            using var response = await client.SendAsync(request);
            Assert.True(response.IsSuccessStatusCode, $"write {i + 1} returned {(int)response.StatusCode}");
        }

        using var overLimit = AuthenticatedLogRequest(token);
        using var rejected = await client.SendAsync(overLimit);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    private static void AssertCarriesPipelineHeaders(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("X-Request-Id", out var requestId));
        Assert.False(string.IsNullOrWhiteSpace(requestId.Single()));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("SAMEORIGIN", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("default-src 'self'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    private static async Task<HttpResponseMessage> SendUntilAsync(
        HttpClient client,
        HttpStatusCode expected,
        Func<HttpRequestMessage> createRequest)
    {
        HttpResponseMessage last;
        var attempts = 0;
        do
        {
            using var request = createRequest();
            last = await client.SendAsync(request);
            attempts++;
        }
        while (last.StatusCode != expected && attempts < 50);

        return last;
    }

    private static HttpRequestMessage AuthenticatedLogRequest(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/logs")
        {
            Content = JsonContent.Create(ValidLog()),
        };
        request.Headers.TryAddWithoutValidation("Cookie", $"{SessionCookie.AccessTokenName}={token}");
        return request;
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
}

// Detrás de un proxy declarado, la partición de la política auth debe salir de la IP
// reenviada: si el limitador corriera antes que ForwardedHeaders, todos los clientes
// compartirían la IP del proxy y se bloquearían entre sí. ForwardedHeadersTests ya fija
// el aislamiento entre dos IPs; aquí se fija el efecto inverso, que muchos clientes
// distintos a través del mismo proxy nunca agotan una cuota común.
[Collection(IntegrationTestGroup.Name)]
public sealed class ForwardedClientPartitionTests(ForwardedHeadersFixture fixture)
    : IClassFixture<ForwardedHeadersFixture>
{
    private const string AuthCallbackUrl = "/api/auth/google/callback?state=x&code=y";

    [Fact]
    public async Task AuthPolicy_BehindTrustedProxy_DoesNotShareOneQuotaAcrossForwardedClients()
    {
        var client = fixture.CreateClient();

        // Más peticiones de las que admite una sola cuota, cada una de un cliente distinto.
        for (var i = 1; i <= fixture.RateLimiting.Auth.PermitLimit + 10; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, AuthCallbackUrl);
            request.Headers.Add(TestRemoteIpStartupFilter.HeaderName, ForwardedHeadersFixture.TrustedProxy);
            request.Headers.Add("X-Forwarded-For", $"192.0.2.{i}");

            using var response = await client.SendAsync(request);

            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }
}
