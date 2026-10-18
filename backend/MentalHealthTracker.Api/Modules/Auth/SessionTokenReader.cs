namespace MentalHealthTracker.Api.Modules.Auth;

// Lee y verifica el JWT de la cookie de sesión UNA sola vez por petición. El limitador de
// escritura (que particiona por usuario) y RequireAuthMiddleware necesitan la misma
// identidad, y verificar es una firma HMAC más un parseo de JSON: el resultado, válido o no,
// se guarda en HttpContext.Items y el segundo consumidor lo reutiliza.
public sealed class SessionTokenReader(JwtService jwtService)
{
    // Clave por instancia de objeto: no puede chocar con ninguna clave de texto de otro componente.
    private static readonly object ItemKey = new();

    public (Guid UserId, string Email)? Read(HttpContext context)
    {
        if (context.Items.TryGetValue(ItemKey, out var cached))
        {
            // Un null guardado significa "ya verificado: no hay sesión válida", no "sin verificar".
            return ((Guid UserId, string Email)?)cached;
        }

        var session = jwtService.Verify(context.Request.Cookies[SessionCookie.AccessTokenName]);
        context.Items[ItemKey] = session;

        return session;
    }
}
