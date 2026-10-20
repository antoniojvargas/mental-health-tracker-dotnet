using MentalHealthTracker.Api.Core.Configuration;
using MentalHealthTracker.Api.Core.Logging;
using MentalHealthTracker.Api.Core.Middleware;
using MentalHealthTracker.Api.Core.OpenApi;
using MentalHealthTracker.Api.Modules.Auth;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.Api.Core.Hosting;

public static class ApiPipeline
{
    // El orden de este pipeline es una decisión de seguridad, no estética:
    //  1. ForwardedHeaders va el primero: la IP y el esquema reales (X-Forwarded-For/Proto)
    //     tienen que estar resueltos antes de que cualquiera los lea. Si el rate limiter
    //     corriera antes, vería la IP del proxy: todos los clientes compartirían un único
    //     cupo (se bloquearían entre sí) y el límite por IP de /api/auth dejaría de frenar
    //     a quien abusa. Solo se activa tras un proxy declarado (ReverseProxy:KnownProxies
    //     / KnownNetworks), porque confiar en esas cabeceras sin proxy permite falsificar la IP.
    //     Justo detrás va el registro de peticiones: al ir fuera del manejador de excepciones anota
    //     el status definitivo de cada respuesta, y al terminar lee la IP ya resuelta.
    //  2. ExceptionHandler envuelve todo lo que viene después, de modo que cualquier fallo
    //     (rate limiter y auth incluidos) sale con el sobre de error y sin filtrar trazas.
    //  3. RequestId y SecurityHeaders antes de cualquier respuesta, para que también las
    //     respuestas cortocircuitadas (429, 401, errores) lleven el id y las cabeceras.
    //  4. CORS antes de Routing/auth: las peticiones preflight se resuelven sin credenciales.
    //  5. RateLimiter antes de la autenticación: se frena la fuerza bruta y el abuso sin gastar
    //     la verificación del JWT ni tocar la base de datos. Necesita Routing antes para
    //     leer los metadatos de política del endpoint.
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        if (app.Services.GetRequiredService<IOptions<ReverseProxyOptions>>().Value.Enabled)
        {
            app.UseForwardedHeaders();
        }

        app.UseRequestLogging();

        app.UseExceptionHandler();
        app.UseRequestId();
        app.UseSecurityHeaders();
        app.UseCors(ApiCors.PolicyName);
        app.UseApiDocumentation(app.Environment);
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }
}
