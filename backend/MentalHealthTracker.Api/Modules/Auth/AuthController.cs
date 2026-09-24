using System.Security.Claims;
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
    AuthCookieOptions cookieOptions,
    IOptions<AppUrlsOptions> appUrls,
    ILogger<AuthController> logger) : ControllerBase
{
    private const string OAuthStateCookieName = "oauth_state";

    private const string LoginErrorRoute = "/login?error=auth_failed";

    [HttpGet("google/callback")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> GoogleCallback(
        [FromQuery] string? state,
        [FromQuery] string? code,
        CancellationToken cancellationToken)
    {
        Response.Cookies.Delete(OAuthStateCookieName, cookieOptions.Create());

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
                cookieOptions.CreateWithMaxAge(JwtService.SessionCookieMaxAge));

            return Redirect($"{appUrls.Value.FrontendUrl}/dashboard");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Google OAuth callback failed");
            return RedirectToLogin();
        }
    }

    [HttpGet("me")]
    [RequireAuth]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var user = await userRepository.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("Session token references user {UserId} that no longer exists", userId);
            return Unauthorized();
        }

        return Ok(new
        {
            id = user.Id,
            email = user.Email,
            name = user.Name,
            avatarUrl = user.AvatarUrl,
        });
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(JwtService.SessionCookieName, cookieOptions.Create());
        return NoContent();
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

    private IActionResult RedirectToLogin() =>
        Redirect($"{appUrls.Value.FrontendUrl}{LoginErrorRoute}");
}