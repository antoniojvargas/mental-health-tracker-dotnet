using System.Security.Claims;
using System.Text.Encodings.Web;
using MentalHealthTracker.Api.Core.Exceptions;
using MentalHealthTracker.Domain.Errors;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.Api.Modules.Auth;

public static class SessionAuthentication
{
    // Esquema de autenticación de la cookie JWT de sesión.
    public const string SchemeName = "Session";

    // Política que aplica [RequireAuth]: autenticarse con ese esquema.
    public const string PolicyName = "RequireSession";
}

// Autentica la petición con el JWT de la cookie de sesión. La verificación la hace
// SessionTokenReader, que la comparte con el limitador de escritura (una sola por petición).
public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    SessionTokenReader sessionTokenReader) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (sessionTokenReader.Read(Context) is not (var userId, var email))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Email, email)],
            authenticationType: SessionAuthentication.SchemeName);

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SessionAuthentication.SchemeName)));
    }

    // El challenge lanza la misma UnauthorizedException que usa /me: el manejador global
    // (UseExceptionHandler, que envuelve a la autorización) la convierte en el sobre de error
    // UNAUTHORIZED, con el mismo cuerpo, log y cabeceras que el resto de errores. Vale también
    // para el negotiate del hub de SignalR, que pasa por el mismo pipeline.
    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        throw new UnauthorizedException("Authentication required");
}
