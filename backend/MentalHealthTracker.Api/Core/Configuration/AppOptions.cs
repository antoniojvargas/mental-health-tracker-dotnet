namespace MentalHealthTracker.Api.Core.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; init; } = string.Empty;
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; init; } = string.Empty;

    public int ExpiresInDays { get; init; } = 7;
}

public sealed class GoogleOAuthOptions
{
    public const string SectionName = "GoogleOAuth";

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string RedirectUri { get; init; } = string.Empty;
}

public sealed class AppUrlsOptions
{
    public const string SectionName = "AppUrls";

    public string FrontendUrl { get; init; } = string.Empty;
}