using System.ComponentModel.DataAnnotations;
using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Api.Modules.DailyLog;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.Api.Core.RateLimiting;

// Única fuente de los límites de las políticas de rate limiting: las políticas, la
// respuesta 429 (cabecera RateLimit-Limit) y los tests leen de aquí. Los valores por
// defecto son el contrato vigente; se pueden sobrescribir con RateLimiting__Auth__PermitLimit,
// RateLimiting__Write__WindowMinutes, etc. Un valor <= 0 tumba el arranque (ValidateOnStart).
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    // GET /api/auth/google/callback, por IP de origen.
    [ValidateObjectMembers]
    public RateLimitPolicyOptions Auth { get; init; } = new() { PermitLimit = 20, WindowMinutes = 15 };

    // POST /api/logs, por id de usuario.
    [ValidateObjectMembers]
    public RateLimitPolicyOptions Write { get; init; } = new() { PermitLimit = 30, WindowMinutes = 15 };

    // Peticiones permitidas por ventana de la política con ese nombre; 0 si no es una
    // política limitada por estas opciones (no hay límite que informar).
    public int PermitLimitFor(string? policyName) => policyName switch
    {
        AuthController.AuthRateLimitPolicy => Auth.PermitLimit,
        DailyLogController.WriteRateLimitPolicy => Write.PermitLimit,
        _ => 0,
    };
}

public sealed class RateLimitPolicyOptions
{
    [Range(1, int.MaxValue, ErrorMessage = "PermitLimit must be greater than 0.")]
    public int PermitLimit { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "WindowMinutes must be greater than 0.")]
    public int WindowMinutes { get; init; }

    public TimeSpan Window => TimeSpan.FromMinutes(WindowMinutes);
}
