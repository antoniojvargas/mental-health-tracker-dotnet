namespace MentalHealthTracker.Domain.Models;

// Perfil público de la cuenta de Google (userinfo del endpoint OAuth), con el
// identificador sub (GoogleId) como clave estable: a diferencia del email, el
// sub nunca cambia, por eso el upsert del usuario se ancla a GoogleId.
public sealed record GoogleProfile(
    string GoogleId,
    string Email,
    string Name,
    string? AvatarUrl);