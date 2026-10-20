namespace MentalHealthTracker.Api.Core.Logging;

// Catálogo de EventId de los logs de la API. Los números son estables: alimentan alertas y
// consultas en el agregador de logs, así que NO se reutilizan ni se renumeran (un log retirado deja
// su hueco). Rangos: 1000 auth, 1100 errores HTTP, 1200 salud, 1300 apagado, 1400 tiempo real.
// Infrastructure tiene su propio catálogo en el rango 2000.
internal static class ApiLogEvents
{
    public const int GoogleCallbackRejected = 1001;

    public const int GoogleCallbackFailed = 1002;

    public const int SessionUserMissing = 1003;

    public const int RequestRejected = 1101;

    public const int UnhandledException = 1102;

    public const int RequestAborted = 1103;

    public const int HealthCheckFailed = 1201;

    public const int ShutdownClosingConnections = 1301;

    public const int ShutdownClosingPool = 1302;

    public const int RealtimeSendFailed = 1401;
}
