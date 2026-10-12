namespace MentalHealthTracker.Api.Modules.Auth;

public static class AuthRoutes
{
    // Ruta absoluta del callback de Google. Es la única fuente: el atributo del
    // controller y la validación de GoogleOAuth:RedirectUri la comparten, para que la
    // URI configurada no pueda apuntar a una ruta que no existe.
    public const string GoogleCallbackPath = "/api/auth/google/callback";
}
