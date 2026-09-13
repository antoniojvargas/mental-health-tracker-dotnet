using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentalHealthTracker.Infrastructure.Persistence;

// En un sistema con múltiples réplicas de la API, aplicar migraciones al arrancar daría
// carreras: varias instancias competirían por migrar el esquema a la vez, con bloqueos
// mutuos o estados parciales. Ahí las migraciones serían un paso de despliegue separado
// (p. ej. un job previo a escalar). Aquí es una simplificación deliberada para una sola
// instancia: la API migra la base de datos antes de empezar a escuchar, y falla en firme
// si no puede, de modo que nunca sirve tráfico contra un esquema desactualizado.
public static class DatabaseInitializer
{
    public static async Task MigrateAsync(AppDbContext dbContext, ILogger logger, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Applying pending database migrations");
        await dbContext.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Database migrations applied");
    }
}