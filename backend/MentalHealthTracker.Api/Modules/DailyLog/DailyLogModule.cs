namespace MentalHealthTracker.Api.Modules.DailyLog;

public static class DailyLogModule
{
    // Lo que el módulo de registros diarios aporta al contenedor: el servicio de aplicación.
    // Sus validadores los registra el escaneo del ensamblado (AddValidatorsFromAssembly, en
    // AddApiCore), los repositorios AddPersistence y el emisor de eventos AddRealtimeModule.
    public static IServiceCollection AddDailyLogModule(this IServiceCollection services)
    {
        services.AddScoped<IDailyLogService, DailyLogService>();

        return services;
    }
}
