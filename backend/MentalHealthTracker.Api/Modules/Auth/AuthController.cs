using MentalHealthTracker.Api.Core.Configuration;
using MentalHealthTracker.Api.Core.Exceptions;
using MentalHealthTracker.Domain.Errors;
using MentalHealthTracker.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.Api.Modules.Auth;

[ApiController]
[Route("api/auth")]
public sealed partial class AuthController(
    GoogleOAuthClient googleOAuthClient,
    IUserRepository userRepository,
    JwtService jwtService,
    AuthCookieOptions cookieOptions,
    OAuthStateService oAuthState,
    IOptions<AppUrlsOptions> appUrls,
    ILogger<AuthController> logger) : ControllerBase
{
    internal const string AuthRateLimitPolicy = "auth";

    private const string LoginErrorRoute = "/login?error=auth_failed";

    // Inicia el flujo OAuth: el navegador navega aquí, recibe una cookie httpOnly con un
    // state aleatorio y un 302 hacia el endpoint de autorización de Google con ese mismo
    // state. OAuthStateService lo compara en el callback en tiempo constante; sin cookie previa la
    // navegación de vuelta se rechaza (login CSRF). Es una navegación de página entera,
    // no un fetch: las cookies la fija el backend, así que el frontend solo enlaza a esta ruta.
    [HttpGet("google")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult GoogleLogin()
    {
        var state = oAuthState.Issue(Response);

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
        var stateMatches = oAuthState.Consume(HttpContext, state);

        if (string.IsNullOrEmpty(code) || !stateMatches)
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
                SessionCookie.AccessTokenName,
                token,
                cookieOptions.CreateWithMaxAge(jwtService.SessionLifetime));

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
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            throw new UnauthorizedException("Authentication required");
        }

        var user = await userRepository.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            LogSessionUserMissing(logger, userId);
            throw new UnauthorizedException("Authentication required");
        }

        return Ok(new UserResponse(user.Id, user.Email, user.Name, user.AvatarUrl));
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(SessionCookie.AccessTokenName, cookieOptions.Create());
        return NoContent();
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