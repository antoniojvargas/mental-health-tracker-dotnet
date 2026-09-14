using System.Security.Claims;
using MentalHealthTracker.Domain.Errors;

namespace MentalHealthTracker.Api.Modules.Auth;

public sealed class RequireAuthMiddleware(RequestDelegate next, JwtService jwtService)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<RequireAuthAttribute>() is not null)
        {
            var token = context.Request.Cookies[JwtService.SessionCookieName];
            if (string.IsNullOrEmpty(token) || jwtService.Verify(token) is not (var userId, var email))
            {
                throw new UnauthorizedException("Authentication required");
            }

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Email, email)],
                authenticationType: "RequireAuth");

            context.User = new ClaimsPrincipal(identity);
        }

        return next(context);
    }
}

public static class RequireAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseRequireAuth(this IApplicationBuilder app) =>
        app.UseMiddleware<RequireAuthMiddleware>();
}