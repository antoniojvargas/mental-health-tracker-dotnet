using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MentalHealthTracker.Infrastructure.Persistence;

// En un sistema con múltiples réplicas de la API, aplicar migraciones al arrancar daría
// carreras: varias instancias competirían por migrar el esquema a la vez, con bloqueos
// mutuos o estados parciales. Ahí las migraciones serían un paso de despliegue separado
// (p. ej. un job previo a escalar). Aquí es una simplificación deliberada para una sola
// instancia: la API migra la base de datos antes de empezar a escuchar, y falla en firme
// si no puede, de modo que nunca sirve tráfico contra un esquema desactualizado.
public static class DatabaseInitializer
{
    private const int MaxAttempts = 10;

    private const int InitialBackoffMs = 1_000;

    private const int MaxBackoffMs = 10_000;

    public static async Task MigrateAsync(AppDbContext dbContext, ILogger logger, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Applying pending database migrations");
        await WaitForDatabaseAsync(dbContext, logger, cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Database migrations applied");
    }

    // La base puede estar reiniciando cuando la API arranca (p. ej. un contenedor que se
    // reinicia tras una caída mientras la API ya está arriba). Abrir la conexión fallaría
    // de inmediato y tiraría todo el proceso; se reintenta con backoff exponencial hasta
    // MaxBackoffMs en cada espera, con un tope de MaxAttempts.
    private static async Task WaitForDatabaseAsync(AppDbContext dbContext, ILogger logger, CancellationToken cancellationToken)
    {
        var connectionString = dbContext.Database.GetConnectionString();
        var backoffMs = InitialBackoffMs;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                return;
            }
            catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
            {
                if (attempt == MaxAttempts)
                {
                    logger.LogError(exception, "Database unreachable after {MaxAttempts} attempts", MaxAttempts);
                    throw;
                }

                logger.LogWarning(
                    exception,
                    "Database not ready (attempt {Attempt}/{MaxAttempts}); retrying in {BackoffMs} ms",
                    attempt,
                    MaxAttempts,
                    backoffMs);
                await Task.Delay(backoffMs, cancellationToken);
                backoffMs = Math.Min(backoffMs * 2, MaxBackoffMs);
            }
        }
    }
}