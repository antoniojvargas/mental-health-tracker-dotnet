using MentalHealthTracker.Api.Core.Serialization;

namespace MentalHealthTracker.Api.Modules.Realtime;

public static class RealtimeModule
{
    // Ruta del hub de registros. El cliente la usa tal cual (frontend/src/services/hub-client.ts,
    // el proxy de nginx y el de Vite reenvían /hub), así que cambiarla rompe el contrato.
    public const string LogsHubPath = "/hub/logs";

    // Lo que el tiempo real aporta al contenedor: SignalR, el seguimiento de conexiones vivas,
    // el emisor de eventos (de mejor esfuerzo, ver LogEventEmitter) y el servicio que cierra las
    // conexiones al apagar el host.
    public static IServiceCollection AddRealtimeModule(this IServiceCollection services)
    {
        services.AddSignalR()
            .AddJsonProtocol(options =>
                options.PayloadSerializerOptions.Converters.Add(new SnakeCaseEnumJsonConverter()));
        services.AddSingleton<SignalRConnectionTracker>();
        services.AddScoped<ILogEventEmitter, LogEventEmitter>();
        services.AddHostedService<Core.Hosting.GracefulShutdownService>();

        return services;
    }

    public static IEndpointRouteBuilder MapRealtimeHubs(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<LogsHub>(LogsHubPath);

        return endpoints;
    }
}
