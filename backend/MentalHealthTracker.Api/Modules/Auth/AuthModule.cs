namespace MentalHealthTracker.Api.Modules.Auth;

public static class AuthModule
{
    // Lo que el módulo de autenticación aporta al contenedor: la firma y verificación del
    // JWT de sesión (con su lector que verifica una vez por petición), las opciones de la cookie, y el cliente HTTP tipado que habla con
    // Google. Las opciones (Jwt, GoogleOAuth, AppUrls) las registra AddAppOptions.
    public static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        services.AddSingleton<JwtService>();
        services.AddSingleton<SessionTokenReader>();
        services.AddSingleton<AuthCookieOptions>();
        services.AddHttpClient<GoogleOAuthClient>();

        return services;
    }

    // Endpoints del módulo que no son del AuthController (ese lo mapea MapControllers).
    // test-login solo existe en el entorno Testing: permite a los e2e entrar sin Google y
    // no debe existir en desarrollo ni en producción.
    public static IEndpointRouteBuilder MapAuthEndpoints(
        this IEndpointRouteBuilder endpoints,
        IHostEnvironment environment)
    {
        if (environment.IsEnvironment("Testing"))
        {
            endpoints.MapTestAuthEndpoints();
        }

        return endpoints;
    }
}
