using MentalHealthTracker.Api.Core.Configuration;
using MentalHealthTracker.Api.Modules.Auth;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.UnitTests;

public sealed class GoogleOAuthOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(string redirectUri) =>
        new GoogleOAuthOptionsValidator().Validate(
            null,
            new GoogleOAuthOptions { RedirectUri = redirectUri });

    [Theory]
    [InlineData("http://localhost:5173/api/auth/google/callback")]
    [InlineData("https://app.example.com/api/auth/google/callback")]
    [InlineData("https://example.com/mht/api/auth/google/callback")]
    public void Validate_AcceptsCallbackPath(string redirectUri)
    {
        Assert.True(Validate(redirectUri).Succeeded);
    }

    [Theory]
    [InlineData("http://localhost:5173/auth/callback")]
    [InlineData("/api/auth/google/callback")]
    [InlineData("ftp://example.com/api/auth/google/callback")]
    [InlineData("https://example.com/api/auth/google/callback/extra")]
    public void Validate_RejectsAndReportsReceivedAndExpected(string redirectUri)
    {
        var result = Validate(redirectUri);

        Assert.True(result.Failed);
        Assert.Contains(redirectUri, result.FailureMessage);
        Assert.Contains(AuthRoutes.GoogleCallbackPath, result.FailureMessage);
    }

    [Fact]
    public void Validate_SkipsEmptyValue_BecauseRequiredAlreadyReportsIt()
    {
        Assert.True(Validate(string.Empty).Succeeded);
    }
}
