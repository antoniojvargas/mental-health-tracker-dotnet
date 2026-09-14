namespace MentalHealthTracker.Api.Modules.Auth;

public sealed class AuthCookieOptions(IHostEnvironment environment)
{
    public CookieOptions Create() => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Secure = environment.IsProduction(),
    };

    public CookieOptions CreateWithMaxAge(TimeSpan maxAge)
    {
        var options = Create();
        options.MaxAge = maxAge;
        return options;
    }
}