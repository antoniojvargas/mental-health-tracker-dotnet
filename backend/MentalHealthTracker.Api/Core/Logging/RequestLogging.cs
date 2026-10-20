using MentalHealthTracker.Api.Core.Middleware;
using Serilog;
using Serilog.Events;

namespace MentalHealthTracker.Api.Core.Logging;

public static class RequestLogging
{
    public const string HealthPath = "/api/health";

    // Una línea por petición (método, ruta sin query, status y duración) con el RequestId y la IP
    // del cliente como propiedades. La IP se lee al TERMINAR la petición, cuando ForwardedHeaders ya
    // sustituyó la del proxy por la del cliente real (por eso su posición respecto a ForwardedHeaders
    // no cambia el resultado). Lo que sí importa es ir FUERA del manejador de excepciones: así ve el
    // status definitivo (el del sobre de error) y no duplica la traza, que ya registra el manejador.
    // /api/health queda excluido: los orquestadores lo sondean cada pocos segundos y no aporta nada
    // (si la base falla, el propio endpoint lo registra como error).
    public static IApplicationBuilder UseRequestLogging(this WebApplication app) =>
        app.UseWhen(
            context => !context.Request.Path.StartsWithSegments(HealthPath, StringComparison.OrdinalIgnoreCase),
            branch => branch.UseSerilogRequestLogging(options =>
            {
                // El logger del host (consola JSON/texto, nivel mínimo, redacción de cabeceras).
                options.Logger = app.Services.GetRequiredService<Serilog.ILogger>();
                options.GetLevel = (context, _, exception) => LevelFor(exception, context.Response.StatusCode);
                options.EnrichDiagnosticContext = (diagnosticContext, context) =>
                {
                    if (context.Items.TryGetValue(RequestIdMiddleware.ItemKey, out var requestId))
                    {
                        diagnosticContext.Set("RequestId", requestId);
                    }

                    diagnosticContext.Set("ClientIp", context.Connection.RemoteIpAddress?.ToString());
                };
            }));

    // 5xx (o una excepción que se escapó) → Error; 4xx → Warning; el resto → Information.
    internal static LogEventLevel LevelFor(Exception? exception, int? status) => (exception, status) switch
    {
        (not null, _) => LogEventLevel.Error,
        (_, >= 500) => LogEventLevel.Error,
        (_, >= 400) => LogEventLevel.Warning,
        _ => LogEventLevel.Information,
    };
}
