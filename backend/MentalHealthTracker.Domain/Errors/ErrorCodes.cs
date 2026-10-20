namespace MentalHealthTracker.Domain.Errors;

// Códigos de error estables del sobre de error de la API (`error.code`). Son contrato con el
// cliente (ApiErrorCode en el frontend): cambiar un valor rompe la gestión de errores allí.
public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";

    public const string Unauthorized = "UNAUTHORIZED";

    public const string NotFound = "NOT_FOUND";

    public const string RateLimited = "RATE_LIMITED";

    public const string InternalError = "INTERNAL_ERROR";
}
