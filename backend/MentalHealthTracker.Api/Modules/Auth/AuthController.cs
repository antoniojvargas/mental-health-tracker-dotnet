using System.Security.Cryptography;
using System.Text;
using MentalHealthTracker.Api.Core.Configuration;
using MentalHealthTracker.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.Api.Modules.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    GoogleOAuthClient googleOAuthClient,
    IUserRepository userRepository,
    JwtService jwtService,
    IOptions<AppUrlsOptions> appUrls,
    ILogger<AuthController> logger) : ControllerBase
{
    private const string OAuthStateCookieName = "oauth_state";

    private static readonly TimeSpan OAuthStateCookieMaxAge = TimeSpan.FromMinutes(10);

    private const string LoginErrorRoute = "/login?error=auth_failed";

    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback(
        [FromQuery] string? state,
        [FromQuery] string? code,
        CancellationToken cancellationToken)
    {
        Response.Cookies.Delete(OAuthStateCookieName, StateCookieOptions());

        if (string.IsNullOrEmpty(code) || !StateMatches(state))
        {
            logger.LogWarning("Google OAuth callback rejected: state cookie missing or mismatch");
            return RedirectToLogin();
        }

        try
        {
            var profile = await googleOAuthClient.ExchangeCodeForProfileAsync(code, cancellationToken);
            var user = await userRepository.UpsertByGoogleIdAsync(profile, cancellationToken);
            var token = jwtService.Sign(user.Id, user.Email);

            Response.Cookies.Append(
                JwtService.SessionCookieName,
                token,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    MaxAge = JwtService.SessionCookieMaxAge,
                });

            return Redirect($"{appUrls.Value.FrontendUrl}/dashboard");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Google OAuth callback failed");
            return RedirectToLogin();
        }
    }

    private bool StateMatches(string? receivedState)
    {
        var expectedState = Request.Cookies[OAuthStateCookieName];
        if (expectedState is null || receivedState is null ||
            expectedState.Length != receivedState.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedState),
            Encoding.UTF8.GetBytes(receivedState));
    }

    private CookieOptions StateCookieOptions() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        MaxAge = OAuthStateCookieMaxAge,
    };

    private IActionResult RedirectToLogin() =>
        Redirect($"{appUrls.Value.FrontendUrl}{LoginErrorRoute}");
}