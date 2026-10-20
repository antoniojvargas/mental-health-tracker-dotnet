using MentalHealthTracker.Api.Core.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
using MentalHealthTracker.Api.Core.Logging;

namespace MentalHealthTracker.Api.Core.Health;

public static partial class HealthEndpoints
{
    public static IServiceCollection AddApplicationUptime(this IServiceCollection services)
    {
        services.AddSingleton<ApplicationUptime>();
        return services;
    }

    // Health check con prueba real de conectividad: devolver un literal "ok" fijo no basta
    // porque los orquestadores (Docker healthcheck, load balancers) usan este endpoint para
    // decidir si el contenedor está sano. Un 200 constante enmascara caídas de la base de
    // datos: no se reiniciaría el contenedor, no habría alertas y los clientes fallarían
    // después con errores confusos. Solo un SELECT 1 real refleja la disponibilidad real.
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // Se resuelve ya al mapear el endpoint (arranque) y no en la primera petición, para
        // que el uptime cuente desde el arranque del host.
        _ = app.ServiceProvider.GetRequiredService<ApplicationUptime>();

        app.MapGet("/api/health", async (
            NpgsqlDataSource dataSource,
            ApplicationUptime applicationUptime,
            ILogger<Program> logger) =>
        {
            var uptime = applicationUptime.Elapsed;

            try
            {
                await using var connection = await dataSource.OpenConnectionAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                await command.ExecuteScalarAsync();
            }
            catch (Exception exception)
            {
                LogHealthCheckFailed(logger, exception);
                return Results.Json(
                    new { status = "error", db = "unreachable" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Json(
                new { status = "ok", uptime = (int)uptime.TotalSeconds });
        });

        return app;
    }

    [LoggerMessage(EventId = ApiLogEvents.HealthCheckFailed, EventName = nameof(ApiLogEvents.HealthCheckFailed), Level = LogLevel.Error, Message = "Database health check failed")]
    private static partial void LogHealthCheckFailed(ILogger logger, Exception exception);
}
