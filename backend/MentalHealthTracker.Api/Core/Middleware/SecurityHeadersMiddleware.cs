namespace MentalHealthTracker.Api.Core.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    // Prefijo de la UI de Swagger (ver options.RoutePrefix en Program.cs). Se mantiene
    // aquí como única fuente de verdad para que la CSP relajada y la ruta no diverjan.
    public const string DocsRoutePrefix = "api/docs";

    private const string ApiContentSecurityPolicy =
        "default-src 'self'; object-src 'none'; frame-ancestors 'self'; base-uri 'self'; form-action 'self'";

    // /api/docs (Swagger UI) necesita una CSP relajada: aunque el HTML que entrega
    // Swashbuckle carga los bundles como archivos externos, la consola inyecta estilos
    // inline en el documento al renderizarse (swagger-ui-bundle) y usa imágenes data:;
    // contra la política estricta esos <style> dinámicos se bloquean y la UI queda rota.
    // Se permite script-src 'unsafe-inline' además, por si el bundle inyecta scripts
    // inline. El resto de la API solo devuelve JSON o un redirect (nunca HTML ejecutable),
    // así que conserva la política estricta.
    private const string DocsContentSecurityPolicy =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; font-src 'self'; connect-src 'self'; object-src 'none'; " +
        "frame-ancestors 'self'; base-uri 'self'; form-action 'self'";

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "SAMEORIGIN";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Content-Security-Policy"] =
            context.Request.Path.StartsWithSegments($"/{DocsRoutePrefix}")
                ? DocsContentSecurityPolicy
                : ApiContentSecurityPolicy;
        return next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();
}