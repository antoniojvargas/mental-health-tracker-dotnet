using MentalHealthTracker.Api.Core.Exceptions;
using MentalHealthTracker.Api.Core.Serialization;
using MentalHealthTracker.Api.Core.Validation;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        // Único reloj de la aplicación: nadie lee el reloj del sistema directamente, así los
        // tests pueden fijar la hora con un TimeProvider falso.
        services.TryAddSingleton(TimeProvider.System);

        services.AddControllers(options =>
            {
                options.Filters.Add<FluentValidationActionFilter>();
            })
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new SnakeCaseEnumJsonConverter()));
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new SnakeCaseEnumJsonConverter()));
        services.AddValidatorsFromAssembly(typeof(ValidatorRegistration).Assembly);
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }
}
