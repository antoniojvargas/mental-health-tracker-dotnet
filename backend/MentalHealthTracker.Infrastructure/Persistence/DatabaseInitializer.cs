using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using MentalHealthTracker.Infrastructure.Logging;

namespace MentalHealthTracker.Infrastructure.Persistence;

// En un sistema con múltiples réplicas de la API, aplicar migraciones al arrancar daría
// carreras: varias instancias competirían por migrar el esquema a la vez, con bloqueos
// mutuos o estados parciales. Ahí las migraciones serían un paso de despliegue separado
// (p. ej. un job previo a escalar). Aquí es una simplificación deliberada para una sola
// instancia: la API migra la base de datos antes de empezar a escuchar, y falla en firme
// si no puede, de modo que nunca sirve tráfico contra un esquema desactualizado.
public static partial class DatabaseInitializer
{
    private const int MaxAttempts = 10;

    private const int InitialBackoffMs = 1_000;

    private const int MaxBackoffMs = 10_000;

    public static async Task MigrateAsync(AppDbContext dbContext, ILogger logger, CancellationToken cancellationToken = default)
    {
        LogApplyingMigrations(logger);
        await WaitForDatabaseAsync(dbContext, logger, cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);
        LogMigrationsApplied(logger);
    }

    // La base puede estar reiniciando cuando la API arranca (p. ej. un contenedor que se
    // reinicia tras una caída mientras la API ya está arriba). Abrir la conexión fallaría
    // de inmediato y tiraría todo el proceso; se reintenta con backoff exponencial hasta
    // MaxBackoffMs en cada espera, con un tope de MaxAttempts.
    private static async Task WaitForDatabaseAsync(AppDbContext dbContext, ILogger logger, CancellationToken cancellationToken)
    {
        var backoffMs = InitialBackoffMs;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // La conexión del propio contexto (con el NpgsqlDataSource de la aplicación): no
                // se reconstruye una cadena, que con un data source llegaría sin contraseña.
                await dbContext.Database.OpenConnectionAsync(cancellationToken);
                await dbContext.Database.CloseConnectionAsync();
                return;
            }
            // SocketException: con Npgsql 9.x un host que no resuelve por DNS (la base aún no
            // existe en la red) llega sin envolver en NpgsqlException. Es el mismo caso de
            // "la base no está lista" y debe reintentarse igual.
            catch (Exception exception) when (IsDatabaseNotReady(exception))
            {
                if (attempt == MaxAttempts)
                {
                    LogDatabaseUnreachable(logger, exception, MaxAttempts);
                    throw;
                }

                LogDatabaseNotReady(logger, exception, attempt, MaxAttempts, backoffMs);
                await Task.Delay(backoffMs, cancellationToken);
                backoffMs = Math.Min(backoffMs * 2, MaxBackoffMs);
            }
        }
    }

    // "La base aún no está lista": un error de Npgsql, un timeout o un fallo de socket. Al abrir la
    // conexión a través de EF, un timeout llega envuelto en una InvalidOperationException
    // ("likely due to a transient failure"), así que se mira también la causa; una
    // InvalidOperationException sin esa causa no es un problema de disponibilidad y no se reintenta.
    internal static bool IsDatabaseNotReady(Exception exception) =>
        exception is NpgsqlException or TimeoutException or SocketException
        || (exception is InvalidOperationException { InnerException: { } inner } && IsDatabaseNotReady(inner));

    [LoggerMessage(EventId = InfrastructureLogEvents.MigrationsApplying, EventName = nameof(InfrastructureLogEvents.MigrationsApplying), Level = LogLevel.Information, Message = "Applying pending database migrations")]
    private static partial void LogApplyingMigrations(ILogger logger);

    [LoggerMessage(EventId = InfrastructureLogEvents.MigrationsApplied, EventName = nameof(InfrastructureLogEvents.MigrationsApplied), Level = LogLevel.Information, Message = "Database migrations applied")]
    private static partial void LogMigrationsApplied(ILogger logger);

    [LoggerMessage(EventId = InfrastructureLogEvents.DatabaseUnreachable, EventName = nameof(InfrastructureLogEvents.DatabaseUnreachable), Level = LogLevel.Error, Message = "Database unreachable after {MaxAttempts} attempts")]
    private static partial void LogDatabaseUnreachable(ILogger logger, Exception exception, int maxAttempts);

    [LoggerMessage(EventId = InfrastructureLogEvents.DatabaseNotReady, EventName = nameof(InfrastructureLogEvents.DatabaseNotReady), Level = LogLevel.Warning, Message = "Database not ready (attempt {Attempt}/{MaxAttempts}); retrying in {BackoffMs} ms")]
    private static partial void LogDatabaseNotReady(ILogger logger, Exception exception, int attempt, int maxAttempts, int backoffMs);
}