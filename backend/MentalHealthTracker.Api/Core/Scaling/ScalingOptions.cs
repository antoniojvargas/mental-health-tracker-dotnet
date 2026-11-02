namespace MentalHealthTracker.Api.Core.Scaling;

// Escalado horizontal opcional. Hoy solo existe Redis, y es UNA decisión con dos efectos (ver
// docs/architecture.md): con Scaling:Redis:ConnectionString se activan a la vez el backplane de
// SignalR y los contadores distribuidos del rate limiting. Vacía (por defecto) = una sola
// instancia, todo en memoria, como siempre.
public sealed class ScalingOptions
{
    public const string SectionName = "Scaling";

    public RedisScalingOptions Redis { get; init; } = new();
}

public sealed class RedisScalingOptions
{
    // Cadena de StackExchange.Redis (p. ej. "redis:6379,password=…"). Puede llevar contraseña:
    // pásala por variable de entorno o secreto del orquestador, no por un fichero versionado.
    public string ConnectionString { get; init; } = string.Empty;

    // Prefijo de los canales de SignalR y de las claves de los contadores, para que varios
    // entornos compartan un Redis sin pisarse.
    public string KeyPrefix { get; init; } = "mht";

    public bool Enabled => !string.IsNullOrWhiteSpace(ConnectionString);
}
