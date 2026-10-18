using MentalHealthTracker.Api.Core.RateLimiting;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.Api.Core.Configuration;

public static class OptionsServiceCollectionExtensions
{
    // Una sección de configuración enlazada a sus opciones, con las anotaciones de datos
    // validadas y la validación forzada al arrancar (ValidateOnStart): un valor ausente o
    // inválido tumba el arranque con un mensaje claro, no la primera petición que lo use.
    public static IServiceCollection AddValidatedOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string section)
        where TOptions : class
    {
        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    // Todas las opciones de la aplicación, en el orden en que se validan al arrancar. Dos
    // de ellas tienen además un validador propio que comprueba lo que las anotaciones no
    // pueden: GoogleOAuth:RedirectUri debe terminar en la ruta del callback, y ReverseProxy
    // debe traer direcciones y redes bien formadas. AddReverseProxyTrust solo traduce
    // ReverseProxyOptions a ForwardedHeadersOptions, por lo que necesita esto registrado.
    public static IServiceCollection AddAppOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddValidatedOptions<DatabaseOptions>(configuration, DatabaseOptions.SectionName);
        services.AddValidatedOptions<JwtOptions>(configuration, JwtOptions.SectionName);

        services.AddValidatedOptions<GoogleOAuthOptions>(configuration, GoogleOAuthOptions.SectionName);
        services.AddSingleton<IValidateOptions<GoogleOAuthOptions>, GoogleOAuthOptionsValidator>();

        services.AddValidatedOptions<AppUrlsOptions>(configuration, AppUrlsOptions.SectionName);

        services.AddValidatedOptions<ReverseProxyOptions>(configuration, ReverseProxyOptions.SectionName);
        services.AddSingleton<IValidateOptions<ReverseProxyOptions>, ReverseProxyOptionsValidator>();

        services.AddValidatedOptions<RateLimitingOptions>(configuration, RateLimitingOptions.SectionName);

        return services;
    }
}
