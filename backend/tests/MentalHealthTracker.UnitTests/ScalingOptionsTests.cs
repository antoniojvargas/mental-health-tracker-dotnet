using MentalHealthTracker.Api.Core.Configuration;
using MentalHealthTracker.Api.Core.Scaling;
using MentalHealthTracker.Api.Modules.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.UnitTests;

// Scaling:Redis es una sola decisión con dos efectos (backplane de SignalR y contadores distribuidos
// del rate limiting). Sin cadena de conexión, que es lo de por defecto, nada cambia respecto a una
// instancia en memoria.
public sealed class ScalingOptionsTests
{
    private static IConfiguration Config(params (string Key, string Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(setting => setting.Key, setting => (string?)setting.Value))
            .Build();

    private static bool UsesRedisBackplane(IServiceCollection services) =>
        services.Any(descriptor =>
            descriptor.ServiceType == typeof(HubLifetimeManager<>)
            && descriptor.ImplementationType == typeof(RedisHubLifetimeManager<>));

    [Fact]
    public void ByDefault_RedisIsDisabled()
    {
        var options = new ScalingOptions();

        Assert.False(options.Redis.Enabled);
        Assert.Equal(string.Empty, options.Redis.ConnectionString);
        Assert.Equal("mht", options.Redis.KeyPrefix);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("redis:6379", true)]
    public void Enabled_DependsOnlyOnTheConnectionString(string connectionString, bool expected)
    {
        Assert.Equal(expected, new RedisScalingOptions { ConnectionString = connectionString }.Enabled);
    }

    [Fact]
    public void TheSection_BindsFromScalingRedis()
    {
        var configuration = Config(
            ("Scaling:Redis:ConnectionString", "redis:6379,password=secret"),
            ("Scaling:Redis:KeyPrefix", "staging"));
        using var provider = new ServiceCollection()
            .AddValidatedOptions<ScalingOptions>(configuration, ScalingOptions.SectionName)
            .BuildServiceProvider();

        var redis = provider.GetRequiredService<IOptions<ScalingOptions>>().Value.Redis;

        Assert.Equal("redis:6379,password=secret", redis.ConnectionString);
        Assert.Equal("staging", redis.KeyPrefix);
    }

    [Theory]
    [InlineData("", "mht", true)]
    [InlineData("redis:6379", "mht", true)]
    [InlineData("redis:6379,abortConnect=false,ssl=true", "mht", true)]
    [InlineData("redis:6379", "", false)]
    [InlineData("redis:6379,unknownOption=1", "mht", false)]
    [InlineData("redis:6379,connectTimeout=not-a-number", "mht", false)]
    public void Validator_AcceptsOnlyAUsableConfiguration(string connectionString, string prefix, bool valid)
    {
        var options = new ScalingOptions { Redis = new RedisScalingOptions { ConnectionString = connectionString, KeyPrefix = prefix } };

        Assert.Equal(valid, new ScalingOptionsValidator().Validate(null, options).Succeeded);
    }

    [Fact]
    public void AddAppOptions_ValidatesTheScalingSection()
    {
        var configuration = Config(
            ("Database:ConnectionString", "Host=localhost;Database=x;Username=x;Password=x"),
            ("Jwt:Secret", "unit-test-jwt-secret-key-value"),
            ("GoogleOAuth:ClientId", "id"),
            ("GoogleOAuth:ClientSecret", "secret"),
            ("GoogleOAuth:RedirectUri", "http://localhost:5173/api/auth/google/callback"),
            ("AppUrls:FrontendUrl", "http://localhost:5173"),
            ("Scaling:Redis:ConnectionString", "redis:6379,unknownOption=1"));
        using var provider = new ServiceCollection().AddAppOptions(configuration).BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<ScalingOptions>>().Value);
    }

    [Fact]
    public void Realtime_WithoutRedis_KeepsTheInMemoryBackplane()
    {
        var services = new ServiceCollection();

        services.AddRealtimeModule(Config());
        var withoutConfiguration = new ServiceCollection();
        withoutConfiguration.AddRealtimeModule();

        Assert.False(UsesRedisBackplane(services));
        Assert.False(UsesRedisBackplane(withoutConfiguration));
    }

    [Fact]
    public void Realtime_WithRedis_RegistersTheRedisBackplane()
    {
        var services = new ServiceCollection();

        services.AddRealtimeModule(Config(("Scaling:Redis:ConnectionString", "redis:6379")));

        Assert.True(UsesRedisBackplane(services));
    }
}
