using Microsoft.AspNetCore.Authorization;

namespace MentalHealthTracker.Api.Modules.Auth;

// Marca un controlador, acción o hub como protegido por la sesión: aplica la política
// RequireSession (esquema de cookie JWT + usuario autenticado). Sigue siendo el único
// atributo que usa el resto del código y que detecta el filtro de OpenAPI.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireAuthAttribute : AuthorizeAttribute
{
    public RequireAuthAttribute()
    {
        Policy = SessionAuthentication.PolicyName;
    }
}
