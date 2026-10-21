using MentalHealthTracker.Api.Modules.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace MentalHealthTracker.UnitTests;

public sealed class OAuthStateServiceTests
{
    private static OAuthStateService CreateService(string environmentName = "Development") =>
        new(new AuthCookieOptions(new FakeEnvironment(environmentName)));

    private static string SetCookie(HttpResponse response) => string.Join("; ", response.Headers.SetCookie.ToArray());

    private static DefaultHttpContext ContextWithStateCookie(string? value)
    {
        var context = new DefaultHttpContext();
        if (value is not null)
        {
            context.Request.Headers.Cookie = $"{SessionCookie.OAuthStateName}={value}";
        }

        return context;
    }

    [Fact]
    public void Issue_ReturnsA32ByteUrlSafeStateAndWritesItInTheCookie()
    {
        var context = new DefaultHttpContext();

        var state = CreateService().Issue(context.Response);

        Assert.Matches("^[A-Za-z0-9_-]{43}$", state);
        Assert.StartsWith($"{SessionCookie.OAuthStateName}={state}", SetCookie(context.Response), StringComparison.Ordinal);
    }

    [Fact]
    public void Issue_WritesAShortLivedHttpOnlyLaxCookie()
    {
        var context = new DefaultHttpContext();

        CreateService().Issue(context.Response);

        var setCookie = SetCookie(context.Response).ToLowerInvariant();
        Assert.Contains("max-age=600", setCookie, StringComparison.Ordinal);
        Assert.Contains("httponly", setCookie, StringComparison.Ordinal);
        Assert.Contains("samesite=lax", setCookie, StringComparison.Ordinal);
        Assert.Contains("path=/", setCookie, StringComparison.Ordinal);
        Assert.DoesNotContain("secure", setCookie, StringComparison.Ordinal);
    }

    [Fact]
    public void Issue_InProduction_MarksTheCookieSecure()
    {
        var context = new DefaultHttpContext();

        CreateService("Production").Issue(context.Response);

        Assert.Contains("secure", SetCookie(context.Response).ToLowerInvariant(), StringComparison.Ordinal);
    }

    [Fact]
    public void Issue_GeneratesADifferentStateEachTime()
    {
        var service = CreateService();

        var states = Enumerable.Range(0, 200)
            .Select(_ => service.Issue(new DefaultHttpContext().Response))
            .ToHashSet();

        Assert.Equal(200, states.Count);
    }

    [Fact]
    public void Consume_WithTheStateFromTheCookie_ReturnsTrue()
    {
        var context = ContextWithStateCookie("abc123");

        Assert.True(CreateService().Consume(context, "abc123"));
    }

    [Theory]
    [InlineData("abc123", "abc124")]
    [InlineData("abc123", "abc12")]
    [InlineData("abc123", "")]
    [InlineData("abc123", null)]
    [InlineData(null, "abc123")]
    [InlineData(null, null)]
    public void Consume_WithAMissingOrDifferentState_ReturnsFalse(string? cookie, string? received)
    {
        var context = ContextWithStateCookie(cookie);

        Assert.False(CreateService().Consume(context, received));
    }

    [Theory]
    [InlineData("abc123", "abc123")]
    [InlineData("abc123", "zzzzzz")]
    [InlineData(null, "abc123")]
    public void Consume_AlwaysDeletesTheCookie_EvenWhenTheComparisonFails(string? cookie, string received)
    {
        var context = ContextWithStateCookie(cookie);

        CreateService().Consume(context, received);

        var setCookie = SetCookie(context.Response).ToLowerInvariant();
        Assert.StartsWith($"{SessionCookie.OAuthStateName}=;", setCookie, StringComparison.Ordinal);
        Assert.Contains("expires=thu, 01 jan 1970", setCookie, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("same", "same", true)]
    [InlineData("same", "Same", false)]
    [InlineData("same", "sam", false)]
    [InlineData("same", "same ", false)]
    [InlineData("", "", true)]
    public void Matches_ComparesExactValues(string expected, string received, bool expectedResult)
    {
        Assert.Equal(expectedResult, OAuthStateService.Matches(expected, received));
    }

    private sealed class FakeEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    // Un state de otra cookie con el mismo valor no vale: solo cuenta la cookie del propio flujo.
    [Fact]
    public void Consume_IgnoresCookiesOtherThanTheOAuthStateOne()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{SessionCookie.AccessTokenName}=abc123";

        Assert.False(CreateService().Consume(context, "abc123"));
    }

    // Misma longitud en caracteres pero distinto contenido (y distinta longitud en bytes UTF-8).
    [Theory]
    [InlineData("aé", "ab")]
    [InlineData("ab", "aé")]
    [InlineData("日本", "日木")]
    public void Matches_WithNonAsciiValuesOfTheSameLength_ComparesTheirBytes(string expected, string received)
    {
        Assert.False(OAuthStateService.Matches(expected, received));
    }

    [Fact]
    public void Matches_WithEqualNonAsciiValues_ReturnsTrue()
    {
        Assert.True(OAuthStateService.Matches("日本-é", "日本-é"));
    }

    [Fact]
    public void Issue_ThenConsume_RoundTripsThroughTheCookie()
    {
        var service = CreateService();
        var issued = new DefaultHttpContext();
        var state = service.Issue(issued.Response);

        var callback = ContextWithStateCookie(state);

        Assert.True(service.Consume(callback, state));
    }
}
