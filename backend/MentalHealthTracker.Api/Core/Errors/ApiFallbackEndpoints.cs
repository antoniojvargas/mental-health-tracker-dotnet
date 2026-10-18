using MentalHealthTracker.Domain.Errors;

namespace MentalHealthTracker.Api.Core.Errors;

public static class ApiFallbackEndpoints
{
    // Cualquier ruta de /api sin endpoint cae aquí y sale por el manejador global con el
    // mismo sobre de error que el resto de la API, en lugar de un 404 vacío. Solo cubre
    // /api: /hub y Swagger quedan fuera. Es el endpoint de menor prioridad, así que no
    // tapa a los reales; por contra, un método no admitido en una ruta existente también
    // termina aquí (404 en vez de 405).
    public static IEndpointRouteBuilder MapApiFallback(this IEndpointRouteBuilder app)
    {
        app.MapFallback("/api/{**path}", (HttpContext context) =>
            throw new NotFoundException($"Route {context.Request.Path} does not exist"));

        return app;
    }
}
