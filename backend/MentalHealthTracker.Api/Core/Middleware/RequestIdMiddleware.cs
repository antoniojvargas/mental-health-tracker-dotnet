using Serilog.Context;

namespace MentalHealthTracker.Api.Core.Middleware;

public sealed class RequestIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Request-Id";

    // Clave en HttpContext.Items: el registro de peticiones, que se emite fuera de este
    // middleware cuando el LogContext ya se cerró, lee de aquí el mismo id.
    public const string ItemKey = "RequestId";

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(requestId))
        {
            requestId = Guid.NewGuid().ToString("N");
        }

        context.Items[ItemKey] = requestId;

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