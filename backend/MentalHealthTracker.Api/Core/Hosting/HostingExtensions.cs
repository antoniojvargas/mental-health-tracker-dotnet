using MentalHealthTracker.Api.Core.Exceptions;
using MentalHealthTracker.Api.Core.Validation;

namespace MentalHealthTracker.Api.Core.Hosting;

public static class HostingExtensions
{
    // Apagado ordenado: SIGTERM para de aceptar conexiones nuevas y da margen a
    // cerrar las conexiones SignalR y el pool de Postgres, pero si algo se queda
    // colgado el host forza la salida al cumplirse los 10 segundos (Kestrel aborta
    // entonces cualquier request/WebSocket pendiente).
    public static IHostBuilder UseGracefulShutdownTimeout(this IHostBuilder hostBuilder)
    {
        return hostBuilder.ConfigureHostOptions(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromSeconds(10);
        });
    }

    public static IServiceCollection AddApiCore(this IServiceCollection services)
    {
        services.AddControllers(options =>
        {
            options.Filters.Add<FluentValidationActionFilter>();
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }
}
