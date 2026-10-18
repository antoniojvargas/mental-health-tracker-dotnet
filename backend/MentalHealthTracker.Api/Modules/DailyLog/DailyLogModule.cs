using FluentValidation;
using MentalHealthTracker.Api.Modules.DailyLog.Dtos;
using MentalHealthTracker.Api.Modules.DailyLog.Validators;

namespace MentalHealthTracker.Api.Modules.DailyLog;

public static class DailyLogModule
{
    // Lo que el módulo de registros diarios aporta al contenedor: un validador por cada
    // petición que valida el filtro global de FluentValidation, y el servicio de aplicación.
    // Los repositorios los registra AddPersistence y el emisor de eventos AddRealtimeModule.
    public static IServiceCollection AddDailyLogModule(this IServiceCollection services)
    {
        services.AddSingleton<IValidator<CreateDailyLogRequest>, CreateDailyLogValidator>();
        services.AddSingleton<IValidator<ListDailyLogsQuery>, ListDailyLogsQueryValidator>();
        services.AddSingleton<IValidator<TodayQuery>, TodayQueryValidator>();
        services.AddScoped<IDailyLogService, DailyLogService>();

        return services;
    }
}
