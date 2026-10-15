using Serilog.Context;

namespace MentalHealthTracker.Api.Core.Middleware;

public sealed class RequestIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Request-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(requestId))
        {
            requestId = Guid.NewGuid().ToString("N");
        }

        // En OnStarting y no al entrar: el manejador global de excepciones reinicia la
        // respuesta y borraría el id justo en los errores, donde más hace falta.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = requestId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("RequestId", requestId))
        {
            await next(context);
        }
    }
}

public static class RequestIdMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestId(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestIdMiddleware>();
}