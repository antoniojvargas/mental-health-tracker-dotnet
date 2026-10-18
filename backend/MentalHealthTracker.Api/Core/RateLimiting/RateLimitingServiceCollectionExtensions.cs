using System.Globalization;
using System.Threading.RateLimiting;
using MentalHealthTracker.Api.Core.Exceptions;
using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Api.Modules.DailyLog;
using Microsoft.AspNetCore.RateLimiting;

namespace MentalHealthTracker.Api.Core.RateLimiting;

public static class RateLimitingServiceCollectionExtensions
{
    // El callback de Google OAuth es el único endpoint que acepta tráfico sin sesión y
    // desencadena una redirección externa + upsert de usuario: sin límite sería un vector
    // de abuso (fuerza bruta sobre state/code y trabajo innecesario contra Google/BD).
    // Se particiona por IP de origen. En desarrollo Kestrel recibe al cliente directamente
    // y RemoteIpAddress es su IP. En producción hay nginx delante, así que RemoteIpAddress
    // sería la del proxy y la cuota se compartiría entre todos los usuarios: con
    // ReverseProxy:Enabled, UseForwardedHeaders (primer middleware) sustituye esa IP por la
    // de X-Forwarded-For, aceptada solo si la petición llega de un proxy conocido.
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.HttpContext.Response.HasStarted)
                {
                    return;
                }

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                var permitLimit = context.HttpContext.GetEndpoint()?
                    .Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName switch
                {
                    AuthController.AuthRateLimitPolicy => 20,
                    DailyLogController.WriteRateLimitPolicy => 30,
                    _ => 0,
                };

                context.HttpContext.Response.Headers["RateLimit-Limit"] =
                    permitLimit.ToString(CultureInfo.InvariantCulture);
                context.HttpContext.Response.Headers["RateLimit-Remaining"] = "0";

                // El limiter fija la metadata RETRY_AFTER con el tiempo restante hasta que se
                // reabra la ventana; es lo que alimenta RateLimit-Reset y Retry-After.
                if (context.Lease.TryGetMetadata("RETRY_AFTER", out var retryAfter) &&
                    retryAfter is TimeSpan retryDelay)
                {
                    var seconds = Math.Max(0, (int)Math.Ceiling(retryDelay.TotalSeconds))
                        .ToString(CultureInfo.InvariantCulture);
                    context.HttpContext.Response.Headers["RateLimit-Reset"] = seconds;
                    context.HttpContext.Response.Headers.RetryAfter = seconds;
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ErrorResponse(new ErrorBody("RATE_LIMITED", "Too many requests. Please try again later.", [])),
                    cancellationToken);
            };

            options.AddPolicy(AuthController.AuthRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(15),
                        AutoReplenishment = true,
                    }));

            // POST /api/logs (upsert de registros) se limita por id de usuario y no por IP:
            // dos pacientes detrás de la misma red (NAT de empresa, campus) no deben consumir
            // la cuota del otro. La partición sale del JWT firmado en la cookie; si el token
            // falta o es inválido no se limita (GetNoLimiter) porque RequireAuth los rechaza
            // igual con 401 y nunca llegan a escribir.
            options.AddPolicy(DailyLogController.WriteRateLimitPolicy, httpContext =>
            {
                var token = httpContext.Request.Cookies[JwtService.SessionCookieName];
                var userId = httpContext.RequestServices
                    .GetRequiredService<JwtService>()
                    .Verify(token)?
                    .UserId;

                if (userId is null)
                {
                    return RateLimitPartition.GetNoLimiter((Guid?)null);
                }

                return RateLimitPartition.GetFixedWindowLimiter(
                    userId,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(15),
                        AutoReplenishment = true,
                    });
            });
        });

        return services;
    }
}
