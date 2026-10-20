namespace MentalHealthTracker.Infrastructure.Logging;

// Catálogo de EventId de los logs de Infrastructure (rango 2000; el de la API es el 1000). Mismas
// reglas: números estables, sin reutilizar ni renumerar. 2000 base de datos, 2100 datos de demostración.
internal static class InfrastructureLogEvents
{
    public const int MigrationsApplying = 2001;

    public const int MigrationsApplied = 2002;

    public const int DatabaseNotReady = 2003;

    public const int DatabaseUnreachable = 2004;

    public const int DemoSeedingStarted = 2101;

    public const int DemoSeedingCompleted = 2102;
}
