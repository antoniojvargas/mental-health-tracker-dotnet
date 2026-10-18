using MentalHealthTracker.Domain.Models;
using MentalHealthTracker.Domain.Repositories;

namespace MentalHealthTracker.Api.Modules.Auth.Testing;

// Endpoint solo para entornos de prueba (ASPNETCORE_ENVIRONMENT=Testing); se registra
// condicionalmente para que no exista en rutas de producción ni desarrollo.
//
// Por qué existe: el flujo OAuth real exige que el navegador visite la pantalla de
// consentimiento de Google y que el backend haga un intercambio servidor-a-servidor del
// code por tokens. Playwright no puede interceptar esa llamada (ocurre dentro del
// backend, fuera del contexto del navegador) ni automatizar de forma fiable la pantalla
// de consentimiento de Google. En su lugar, este endpoint replica el resultado del
// callback: hace el mismo upsert del usuario y emite exactamente la misma cookie de
// sesión (mismo JwtService y mismas opciones de cookie), para que los e2e entren
// autenticados sin depender de Google.
public static class TestAuthEndpoints
{
    public const string EnvironmentName = "Testing";

    // Falla el arranque si algo intenta mapear el endpoint fuera de Testing: un test-login
    // que emite sesiones sin pasar por Google en desarrollo o en producción sería una puerta
    // trasera, así que no basta con que el llamador habitual compruebe el entorno.
    public static void MapTestAuthEndpoints(this IEndpointRouteBuilder app, IHostEnvironment environment)
    {
        if (!environment.IsEnvironment(EnvironmentName))
        {
            throw new InvalidOperationException(
                $"The test-login endpoint can only be mapped in the '{EnvironmentName}' environment, " +
                $"but the current environment is '{environment.EnvironmentName}'.");
        }

        app.MapPost("/api/auth/test-login", async (
            TestLoginRequest request,
            IUserRepository userRepository,
            JwtService jwtService,
            AuthCookieOptions cookieOptions,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest();
            }

            var profile = new GoogleProfile(
                GoogleId: $"e2e-{request.Email}",
                Email: request.Email,
                Name: request.Name,
                AvatarUrl: null);

            var user = await userRepository.UpsertByGoogleIdAsync(profile, cancellationToken);
            var token = jwtService.Sign(user.Id, user.Email);

            httpContext.Response.Cookies.Append(
                SessionCookie.AccessTokenName,
                token,
                cookieOptions.CreateWithMaxAge(jwtService.SessionLifetime));

            return Results.NoContent();
        });
    }
}

public sealed record TestLoginRequest(string Email, string Name);