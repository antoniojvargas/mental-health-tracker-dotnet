using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using MentalHealthTracker.Api.Core.RateLimiting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.IntegrationTests;

// Con Scaling:Redis configurado, las cuotas viven en un almacén compartido y no en la memoria de
// cada instancia. Aquí el almacén es un doble en memoria compartido por dos hosts, que hacen de dos
// réplicas: lo que se prueba es el cableado (qué clave se cuenta, qué pasa cuando se agota la cuota
// o cuando el almacén falla) y no Redis, cuyo script se verificó contra un servidor real.
[Collection(IntegrationTestGroup.Name)]
public sealed class DistributedRateLimitingTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const string AuthCallbackUrl = "/api/auth/google/callback?state=x&code=y";
    private const string TrustedProxy = "10.0.0.2";

    private int AuthPermitLimit => Factory.Services.GetRequiredService<IOptions<RateLimitingOptions>>().Value.Auth.PermitLimit;
    private int WritePermitLimit => Factory.Services.GetRequiredService<IOptions<RateLimitingOptions>>().Value.Write.PermitLimit;

    [Fact]
    public async Task TwoInstances_ShareTheAuthQuota()
    {
        var store = new FakeCounterStore();
        await using var instanceA = Replica(store);
        await using var instanceB = Replica(store);
        var clientA = Client(instanceA);
        var clientB = Client(instanceB);

        for (var i = 0; i < AuthPermitLimit; i++)
        {
            var response = await Send(i % 2 == 0 ? clientA : clientB, "203.0.113.1");
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        // Cada instancia solo recibió la mitad de las peticiones: sin almacén compartido ninguna
        // habría llegado al límite.
        var rejectedOnA = await Send(clientA, "203.0.113.1");
        var rejectedOnB = await Send(clientB, "203.0.113.1");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedOnA.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedOnB.StatusCode);
    }

    [Fact]
    public async Task TheRejection_KeepsTheHttpContract()
    {
        var store = new FakeCounterStore();
        await using var instance = Replica(store);
        var client = Client(instance);

        for (var i = 0; i < AuthPermitLimit; i++)
        {
            await Send(client, "203.0.113.2");
        }

        var rejected = await Send(client, "203.0.113.2");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(AuthPermitLimit.ToString(CultureInfo.InvariantCulture), Single(rejected, "RateLimit-Limit"));
        Assert.Equal("0", Single(rejected, "RateLimit-Remaining"));
        Assert.True(int.Parse(Single(rejected, "RateLimit-Reset"), CultureInfo.InvariantCulture) > 0);
        Assert.Equal(Single(rejected, "RateLimit-Reset"), Single(rejected, "Retry-After"));
        Assert.Contains("RATE_LIMITED", await rejected.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AuthPartition_UsesTheRealClientIpResolvedByForwardedHeaders_NotTheProxyOne()
    {
        var store = new FakeCounterStore();
        await using var instance = Replica(store);
        var client = Client(instance);

        await Send(client, "203.0.113.50");

        var key = Assert.Single(store.Keys);
        Assert.Equal("mht:rl:auth:203.0.113.50", key);
        Assert.DoesNotContain(TrustedProxy, key);
    }

    [Fact]
    public async Task AuthPartition_FromAnUntrustedAddress_IgnoresForwardedFor()
    {
        var store = new FakeCounterStore();
        await using var instance = Replica(store);
        var client = Client(instance);

        await Send(client, "203.0.113.60", remoteAddress: "198.51.100.9");

        Assert.Equal("mht:rl:auth:198.51.100.9", Assert.Single(store.Keys));
    }

    [Fact]
    public async Task DifferentClientIps_DoNotShareAQuota()
    {
        var store = new FakeCounterStore();
        await using var instance = Replica(store);
        var client = Client(instance);

        for (var i = 0; i < AuthPermitLimit; i++)
        {
            await Send(client, "203.0.113.70");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await Send(client, "203.0.113.70")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Send(client, "203.0.113.71")).StatusCode);
    }

    [Fact]
    public async Task WritePolicy_IsKeyedByUserIdAndSharedAcrossInstances()
    {
        var (user, token) = await CreateSessionTokenAsync();
        var store = new FakeCounterStore();
        await using var instanceA = Replica(store);
        await using var instanceB = Replica(store);
        var clientA = Client(instanceA);
        var clientB = Client(instanceB);

        for (var i = 0; i < WritePermitLimit; i++)
        {
            var response = await PostLog(i % 2 == 0 ? clientA : clientB, token);
            Assert.True(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK, $"Recibió {response.StatusCode}");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await PostLog(clientA, token)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await PostLog(clientB, token)).StatusCode);
        Assert.Equal($"mht:rl:write:{user.Id}", Assert.Single(store.Keys));
    }

    [Fact]
    public async Task WithoutScalingRedis_TheStoreIsNeverUsedAndTheQuotaStaysLocal()
    {
        var store = new FakeCounterStore();
        await using var instance = Replica(store, redisConfigured: false);
        var client = Client(instance);

        for (var i = 0; i < AuthPermitLimit; i++)
        {
            Assert.Equal(HttpStatusCode.Redirect, (await Send(client, "203.0.113.80")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await Send(client, "203.0.113.80")).StatusCode);
        Assert.Empty(store.Keys);
    }

    [Fact]
    public async Task WhenTheStoreFails_RequestsGoThroughInsteadOfFailing()
    {
        var store = new FakeCounterStore { Fail = true };
        await using var instance = Replica(store);
        var client = Client(instance);

        for (var i = 0; i < AuthPermitLimit + 3; i++)
        {
            Assert.Equal(HttpStatusCode.Redirect, (await Send(client, "203.0.113.90")).StatusCode);
        }
    }

    private WebApplicationFactory<Program> Replica(FakeCounterStore store, bool redisConfigured = true) =>
        Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["ReverseProxy:Enabled"] = "true",
                    ["ReverseProxy:KnownProxies:0"] = TrustedProxy,
                };
                if (redisConfigured)
                {
                    settings["Scaling:Redis:ConnectionString"] = "redis.invalid:6379";
                }

                configuration.AddInMemoryCollection(settings);
            });

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IRateLimitCounterStore>(store);
                services.AddSingleton<Microsoft.AspNetCore.Hosting.IStartupFilter, TestRemoteIpStartupFilter>();
            });
        });

    private static HttpClient Client(WebApplicationFactory<Program> instance) =>
        instance.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static Task<HttpResponseMessage> Send(HttpClient client, string forwardedFor, string remoteAddress = TrustedProxy)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, AuthCallbackUrl);
        request.Headers.Add(TestRemoteIpStartupFilter.HeaderName, remoteAddress);
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostLog(HttpClient client, string token) =>
        client.SendAuthenticatedAsync(HttpMethod.Post, "/api/logs", token, JsonBody(new CreateDailyLogRequestBuilder().Build()));

    private static string Single(HttpResponseMessage response, string name)
    {
        Assert.True(response.Headers.TryGetValues(name, out var values), $"Falta la cabecera {name}");
        return Assert.Single(values!);
    }

    // Almacén en memoria compartido entre las "réplicas" del test. Es un contador de ventana fija
    // igual que el de Redis; Fail simula que Redis no responde.
    private sealed class FakeCounterStore : IRateLimitCounterStore
    {
        private readonly ConcurrentDictionary<string, (long Count, DateTimeOffset Expires)> _counters = new();

        public bool Fail { get; init; }

        public IReadOnlyCollection<string> Keys => [.. _counters.Keys];

        public RateLimitCount Increment(string key, int by, TimeSpan window)
        {
            if (Fail)
            {
                throw new InvalidOperationException("Redis is down");
            }

            var now = DateTimeOffset.UtcNow;
            var entry = _counters.AddOrUpdate(
                key,
                _ => (by, now + window),
                (_, current) => current.Expires <= now ? (by, now + window) : (current.Count + by, current.Expires));
            return new RateLimitCount(entry.Count, entry.Expires - now);
        }

        public Task<RateLimitCount> IncrementAsync(string key, int by, TimeSpan window, CancellationToken cancellationToken) =>
            Task.FromResult(Increment(key, by, window));
    }
}
