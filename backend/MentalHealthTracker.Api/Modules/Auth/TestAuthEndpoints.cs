using MentalHealthTracker.Domain.Models;
using MentalHealthTracker.Domain.Repositories;

namespace MentalHealthTracker.Api.Modules.Auth;

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
    public static void MapTestAuthEndpoints(this IEndpointRouteBuilder app)
    {
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
                JwtService.SessionCookieName,
                token,
                cookieOptions.CreateWithMaxAge(JwtService.SessionCookieMaxAge));

            return Results.NoContent();
        });
    }
}

public sealed record TestLoginRequest(string Email, string Name);