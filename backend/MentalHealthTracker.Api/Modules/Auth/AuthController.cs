using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MentalHealthTracker.Api.Core.Configuration;
using MentalHealthTracker.Api.Core.Exceptions;
using MentalHealthTracker.Domain.Errors;
using MentalHealthTracker.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.Api.Modules.Auth;

[ApiController]
[Route("api/auth")]
public sealed partial class AuthController(
    GoogleOAuthClient googleOAuthClient,
    IUserRepository userRepository,
    JwtService jwtService,
    AuthCookieOptions cookieOptions,
    IOptions<AppUrlsOptions> appUrls,
    ILogger<AuthController> logger) : ControllerBase
{
    internal const string AuthRateLimitPolicy = "auth";

    private const string OAuthStateCookieName = "oauth_state";

    private static readonly TimeSpan OAuthStateCookieMaxAge = TimeSpan.FromMinutes(10);

    private const string LoginErrorRoute = "/login?error=auth_failed";

    // Inicia el flujo OAuth: el navegador navega aquí, recibe una cookie httpOnly con un
    // state aleatorio y un 302 hacia el endpoint de autorización de Google con ese mismo
    // state. El state se compara en el callback con FixedTimeEquals; sin cookie previa la
    // navegación de vuelta se rechaza (login CSRF). Es una navegación de página entera,
    // no un fetch: las cookies la fija el backend, así que el frontend solo enlaza a esta ruta.
    [HttpGet("google")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult GoogleLogin()
    {
        var state = GenerateState();

        Response.Cookies.Append(
            OAuthStateCookieName,
            state,
            cookieOptions.CreateWithMaxAge(OAuthStateCookieMaxAge));

        return Redirect(googleOAuthClient.BuildAuthUrl(state));
    }

    [HttpGet(AuthRoutes.GoogleCallbackPath)]
    [EnableRateLimiting(AuthRateLimitPolicy)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GoogleCallback(
        [FromQuery] string? state,
        [FromQuery] string? code,
        CancellationToken cancellationToken)
    {
        Response.Cookies.Delete(OAuthStateCookieName, cookieOptions.Create());

        if (string.IsNullOrEmpty(code) || !StateMatches(state))
        {
            LogCallbackRejected(logger);
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
            LogCallbackFailed(logger, exception);
            return RedirectToLogin();
        }
    }

    [HttpGet("me")]
    [RequireAuth]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedException("Authentication required");
        }

        var user = await userRepository.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            LogSessionUserMissing(logger, userId);
            throw new UnauthorizedException("Authentication required");
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
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
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

    private static string GenerateState()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    private RedirectResult RedirectToLogin() =>
        Redirect($"{appUrls.Value.FrontendUrl}{LoginErrorRoute}");

    [LoggerMessage(Level = LogLevel.Warning, Message = "Google OAuth callback rejected: state cookie missing or mismatch")]
    private static partial void LogCallbackRejected(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Google OAuth callback failed")]
    private static partial void LogCallbackFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Session token references user {UserId} that no longer exists")]
    private static partial void LogSessionUserMissing(ILogger logger, Guid userId);
}