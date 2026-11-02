using MentalHealthTracker.Api.Core.Logging;

namespace MentalHealthTracker.Api.Core.Health;

public static partial class HealthEndpoints
{
    public const string HealthPath = "/api/health";

    public const string LivePath = "/api/health/live";

    public const string ReadyPath = "/api/health/ready";

    // Tiempo máximo que el health check espera a la base de datos. Un orquestador que sondea tiene
    // su propio timeout: si la base se cuelga y el endpoint no contesta, el sondeo expira sin
    // respuesta; con este tope contesta el mismo 503 de siempre.
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);

    public static IServiceCollection AddApplicationHealth(this IServiceCollection services)
    {
        services.AddSingleton<ApplicationUptime>();
        services.AddSingleton<IDatabaseProbe, NpgsqlDatabaseProbe>(provider =>
            new NpgsqlDatabaseProbe(provider.GetRequiredService<Npgsql.NpgsqlDataSource>()));
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

        // /api/health sigue igual (es el que usan los healthchecks de Compose y del CI) y comparte con
        // /api/health/ready la misma comprobación de la base, con la misma respuesta.
        app.MapGet(HealthPath, CheckDatabaseAsync);

        // Liveness: el proceso responde. No toca la base a propósito: una base caída no debe hacer
        // que un orquestador reinicie un proceso sano.
        app.MapGet(LivePath, (ApplicationUptime applicationUptime) =>
            Results.Json(new { status = "ok", uptime = (int)applicationUptime.Elapsed.TotalSeconds }));

        // Readiness: la base responde (SELECT 1 con el mismo timeout). Un 503 saca a la réplica del
        // balanceo sin reiniciarla.
        app.MapGet(ReadyPath, CheckDatabaseAsync);

        return app;
    }

    private static async Task<IResult> CheckDatabaseAsync(
        IDatabaseProbe databaseProbe,
        ApplicationUptime applicationUptime,
        ILogger<Program> logger,
        CancellationToken requestAborted)
    {
        var uptime = applicationUptime.Elapsed;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        timeout.CancelAfter(ProbeTimeout);

        try
        {
            await databaseProbe.ProbeAsync(timeout.Token);
        }
        // Si quien cancela es el cliente (cerró la conexión) no hay a quién contestar ni
        // es un fallo de la base: la excepción sube al manejador global, que lo anota en Debug.
        catch (OperationCanceledException) when (requestAborted.IsCancellationRequested)
        {
            throw;
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
    }

    [LoggerMessage(EventId = ApiLogEvents.HealthCheckFailed, EventName = nameof(ApiLogEvents.HealthCheckFailed), Level = LogLevel.Error, Message = "Database health check failed")]
    private static partial void LogHealthCheckFailed(ILogger logger, Exception exception);
}
