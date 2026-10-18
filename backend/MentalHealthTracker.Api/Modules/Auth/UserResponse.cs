namespace MentalHealthTracker.Api.Modules.Auth;

// Usuario autenticado que devuelve GET /api/auth/me. Describe el JSON que la ruta ya
// devolvía (id, email, name, avatarUrl en camelCase) para que el documento OpenAPI lo recoja.
public sealed record UserResponse(Guid Id, string Email, string Name, string? AvatarUrl);
