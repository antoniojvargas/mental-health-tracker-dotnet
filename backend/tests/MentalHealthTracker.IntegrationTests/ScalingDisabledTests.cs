using System.Net;
using MentalHealthTracker.Api.Core.RateLimiting;
using MentalHealthTracker.Api.Core.Scaling;
using MentalHealthTracker.Api.Modules.Realtime;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.IntegrationTests;

// Sin Scaling:Redis la aplicación es la de una sola instancia de siempre: SignalR usa su gestor en
// memoria y las cuotas se cuentan en la instancia. Es el comportamiento por defecto y el de los
// despliegues que no configuran Redis, así que se fija con la aplicación completa y no solo con el
// registro de servicios.
[Collection(IntegrationTestGroup.Name)]
public sealed class ScalingDisabledTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const string AuthCallbackUrl = "/api/auth/google/callback?state=x&code=y";

    [Fact]
    public void ByDefault_RedisIsNotConfigured()
    {
        var redis = Factory.Services.GetRequiredService<IOptions<ScalingOptions>>().Value.Redis;

        Assert.False(redis.Enabled);
        Assert.Equal(string.Empty, redis.ConnectionString);
    }

    [Fact]
    public void WithoutScalingRedis_SignalRUsesTheInMemoryLifetimeManager_NotARedisBackplane()
    {
        var manager = Factory.Services.GetRequiredService<HubLifetimeManager<LogsHub>>();

        Assert.IsType<DefaultHubLifetimeManager<LogsHub>>(manager);
        Assert.DoesNotContain("Redis", manager.GetType().FullName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutScalingRedis_TheQuotaIsCountedInMemory_AndTheCounterStoreIsNeverUsed()
    {
        var store = new StoreThatMustNotBeUsed();
        await using var host = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IRateLimitCounterStore>(store)));
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var permitLimit = host.Services.GetRequiredService<IOptions<RateLimitingOptions>>().Value.Auth.PermitLimit;

        for (var i = 0; i < permitLimit; i++)
        {
            Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(AuthCallbackUrl)).StatusCode);
        }

        // Si la cuota se hubiera contado en otro sitio, esta petición no sería un 429 local.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync(AuthCallbackUrl)).StatusCode);
        Assert.Equal(0, store.Calls);
    }

    private sealed class StoreThatMustNotBeUsed : IRateLimitCounterStore
    {
        public int Calls { get; private set; }

        public RateLimitCount Increment(string key, int by, TimeSpan window)
        {
            Calls++;
            throw new InvalidOperationException("The counter store must not be used without Scaling:Redis.");
        }

        public Task<RateLimitCount> IncrementAsync(string key, int by, TimeSpan window, CancellationToken cancellationToken) =>
            Task.FromResult(Increment(key, by, window));
    }
}
