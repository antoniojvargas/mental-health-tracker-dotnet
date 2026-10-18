using MentalHealthTracker.Api.Core.Configuration;
using MentalHealthTracker.Api.Core.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.UnitTests;

public sealed class RateLimitingOptionsTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddAppOptions(configuration).BuildServiceProvider();
    }

    private static Dictionary<string, string?> ValidSettings() => new()
    {
        ["Database:ConnectionString"] = "Host=localhost;Database=x;Username=x;Password=x",
        ["Jwt:Secret"] = "unit-test-jwt-secret-key-value",
        ["GoogleOAuth:ClientId"] = "client-id",
        ["GoogleOAuth:ClientSecret"] = "client-secret",
        ["GoogleOAuth:RedirectUri"] = "http://localhost:5173/api/auth/google/callback",
        ["AppUrls:FrontendUrl"] = "http://localhost:5173",
    };

    [Fact]
    public void WithoutConfiguration_UsesTheCurrentContract()
    {
        using var provider = Build(ValidSettings());

        var options = provider.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

        Assert.Equal(20, options.Auth.PermitLimit);
        Assert.Equal(TimeSpan.FromMinutes(15), options.Auth.Window);
        Assert.Equal(30, options.Write.PermitLimit);
        Assert.Equal(TimeSpan.FromMinutes(15), options.Write.Window);
    }

    [Fact]
    public void ConfiguredValues_OverrideTheDefaults()
    {
        var settings = ValidSettings();
        settings["RateLimiting:Auth:PermitLimit"] = "5";
        settings["RateLimiting:Write:WindowMinutes"] = "1";
        using var provider = Build(settings);

        var options = provider.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

        Assert.Equal(5, options.Auth.PermitLimit);
        Assert.Equal(TimeSpan.FromMinutes(15), options.Auth.Window);
        Assert.Equal(30, options.Write.PermitLimit);
        Assert.Equal(TimeSpan.FromMinutes(1), options.Write.Window);
    }

    [Theory]
    [InlineData("RateLimiting:Auth:PermitLimit", "0")]
    [InlineData("RateLimiting:Auth:PermitLimit", "-1")]
    [InlineData("RateLimiting:Auth:WindowMinutes", "0")]
    [InlineData("RateLimiting:Write:PermitLimit", "0")]
    [InlineData("RateLimiting:Write:WindowMinutes", "-5")]
    public void NonPositiveValues_AreRejected(string key, string value)
    {
        var settings = ValidSettings();
        settings[key] = value;
        using var provider = Build(settings);

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<RateLimitingOptions>>().Value);
    }

    [Theory]
    [InlineData("auth", 20)]
    [InlineData("write", 30)]
    [InlineData("other", 0)]
    [InlineData(null, 0)]
    public void PermitLimitFor_ReturnsTheLimitOfTheNamedPolicy(string? policyName, int expected)
    {
        Assert.Equal(expected, new RateLimitingOptions().PermitLimitFor(policyName));
    }
}
