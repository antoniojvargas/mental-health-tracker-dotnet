using MentalHealthTracker.Api.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.UnitTests;

// AddAppOptions debe dejar cada sección enlazada Y validada: un valor inválido hace fallar
// la resolución de las opciones (y, con ValidateOnStart, el arranque). Esto cubre el
// registro; las reglas de cada validador tienen sus propios tests.
public sealed class OptionsServiceCollectionExtensionsTests
{
    private static Dictionary<string, string?> ValidSettings() => new()
    {
        ["Database:ConnectionString"] = "Host=localhost;Database=x;Username=x;Password=x",
        ["Jwt:Secret"] = "unit-test-jwt-secret-key-value",
        ["GoogleOAuth:ClientId"] = "client-id",
        ["GoogleOAuth:ClientSecret"] = "client-secret",
        ["GoogleOAuth:RedirectUri"] = "http://localhost:5173/api/auth/google/callback",
        ["AppUrls:FrontendUrl"] = "http://localhost:5173",
    };

    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddAppOptions(configuration).BuildServiceProvider();
    }

    [Fact]
    public void ValidSettings_BindEveryOptionsType()
    {
        using var provider = Build(ValidSettings());

        Assert.Equal("client-id", provider.GetRequiredService<IOptions<GoogleOAuthOptions>>().Value.ClientId);
        Assert.Equal("http://localhost:5173", provider.GetRequiredService<IOptions<AppUrlsOptions>>().Value.FrontendUrl);
        Assert.Equal("unit-test-jwt-secret-key-value", provider.GetRequiredService<IOptions<JwtOptions>>().Value.Secret);
        Assert.StartsWith("Host=localhost", provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString);
        Assert.False(provider.GetRequiredService<IOptions<ReverseProxyOptions>>().Value.Enabled);
    }

    [Theory]
    [InlineData("Database:ConnectionString", "")]
    [InlineData("Jwt:Secret", "short")]
    [InlineData("GoogleOAuth:ClientId", "")]
    [InlineData("AppUrls:FrontendUrl", "")]
    public void DataAnnotationViolations_AreRejected(string key, string value)
    {
        var settings = ValidSettings();
        settings[key] = value;
        using var provider = Build(settings);

        var section = key.Split(':')[0];
        Assert.Throws<OptionsValidationException>(() => ResolveSection(provider, section));
    }

    [Fact]
    public void RedirectUri_NotEndingInTheCallbackPath_IsRejectedByItsValidator()
    {
        var settings = ValidSettings();
        settings["GoogleOAuth:RedirectUri"] = "http://localhost:5173/callback";
        using var provider = Build(settings);

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<GoogleOAuthOptions>>().Value);
        Assert.Contains("/api/auth/google/callback", exception.Message);
    }

    [Fact]
    public void ReverseProxy_WithAMalformedNetwork_IsRejectedByItsValidator()
    {
        var settings = ValidSettings();
        settings["ReverseProxy:Enabled"] = "true";
        settings["ReverseProxy:KnownNetworks:0"] = "not-a-network";
        using var provider = Build(settings);

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ReverseProxyOptions>>().Value);
        Assert.Contains("not-a-network", exception.Message);
    }

    [Fact]
    public void AddValidatedOptions_BindsAndValidatesOneSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Secret"] = "short" })
            .Build();
        using var provider = new ServiceCollection()
            .AddValidatedOptions<JwtOptions>(configuration, JwtOptions.SectionName)
            .BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<JwtOptions>>().Value);
    }

    private static object ResolveSection(ServiceProvider provider, string section) => section switch
    {
        "Database" => provider.GetRequiredService<IOptions<DatabaseOptions>>().Value,
        "Jwt" => provider.GetRequiredService<IOptions<JwtOptions>>().Value,
        "GoogleOAuth" => provider.GetRequiredService<IOptions<GoogleOAuthOptions>>().Value,
        "AppUrls" => provider.GetRequiredService<IOptions<AppUrlsOptions>>().Value,
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, null),
    };
}
