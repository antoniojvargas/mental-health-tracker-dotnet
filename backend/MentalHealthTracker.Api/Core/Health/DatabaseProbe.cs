using Npgsql;

namespace MentalHealthTracker.Api.Core.Health;

// La prueba de conectividad con la base de datos que usa /api/health. Va detrás de una interfaz
// para poder probar el timeout del endpoint con una base "colgada" sin colgar de verdad ninguna.
public interface IDatabaseProbe
{
    Task ProbeAsync(CancellationToken cancellationToken);
}

// SELECT 1 real contra el NpgsqlDataSource compartido. Además del token (el endpoint lo cancela a
// los 2 s), el comando lleva su propio CommandTimeout: así una consulta que el servidor no contesta
// tampoco se queda esperando aunque alguien llame a la prueba sin token.
public sealed class NpgsqlDatabaseProbe(NpgsqlDataSource dataSource, string sql = "SELECT 1") : IDatabaseProbe
{
    // En segundos, como CommandTimeout.
    public const int CommandTimeoutSeconds = 2;

    public async Task ProbeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = CommandTimeoutSeconds;
        await command.ExecuteScalarAsync(cancellationToken);
    }
}
