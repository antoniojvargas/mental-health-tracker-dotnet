using System.Security.Claims;

namespace MentalHealthTracker.Api.Modules.Auth;

public static class ClaimsPrincipalExtensions
{
    // El id del usuario viaja en el claim NameIdentifier que RequireAuthMiddleware construye a
    // partir del JWT verificado. Devuelve false si el principal falta o el claim no existe o
    // no es un Guid; cada llamador decide qué hacer en ese caso.
    public static bool TryGetUserId(this ClaimsPrincipal? principal, out Guid userId)
    {
        userId = Guid.Empty;
        var claim = principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        return claim is not null && Guid.TryParse(claim, out userId);
    }
}
