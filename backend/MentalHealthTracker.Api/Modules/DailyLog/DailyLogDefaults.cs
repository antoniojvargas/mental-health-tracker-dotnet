namespace MentalHealthTracker.Api.Modules.DailyLog;

// Límites y valores por defecto del módulo de registros diarios: una sola fuente para el
// DTO de consulta, los validadores y el servicio.
public static class DailyLogDefaults
{
    // Ventana del listado cuando no se indica from/to: los últimos 30 días, hoy incluido.
    public const int DefaultRangeDays = 30;

    public const int DefaultLimit = 100;

    public const int MaxLimit = 366;

    public const int MaxRangeDays = 366;

    // El día lo decide el cliente en su zona horaria: UTC+14 es la más adelantada, donde ya
    // es "mañana" respecto al día UTC, así que se admite hasta UTC + 1 día.
    public const int MaxClientDayAheadOfUtc = 1;
}
