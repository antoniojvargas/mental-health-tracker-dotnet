namespace MentalHealthTracker.Api.Modules.Auth;

// Nombres de las cookies del flujo de autenticación: una sola definición para quien las
// escribe (controller, test-login), quien las lee (middleware, rate limiting) y para el
// esquema de seguridad del documento OpenAPI.
public static class SessionCookie
{
    // Sesión: el JWT firmado, httpOnly.
    public const string AccessTokenName = "access_token";

    // Estado anti login-CSRF del flujo OAuth: vive solo entre /api/auth/google y el callback.
    public const string OAuthStateName = "oauth_state";
}
