using System.ComponentModel.DataAnnotations;

namespace MentalHealthTracker.Api.Core.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required(ErrorMessage = "Database:ConnectionString is required.")]
    public string ConnectionString { get; init; } = string.Empty;
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required(ErrorMessage = "Jwt:Secret is required.")]
    [MinLength(16, ErrorMessage = "Jwt:Secret must be at least 16 characters.")]
    public string Secret { get; init; } = string.Empty;

    public int ExpiresInDays { get; init; } = 7;
}

public sealed class GoogleOAuthOptions
{
    public const string SectionName = "GoogleOAuth";

    [Required(ErrorMessage = "GoogleOAuth:ClientId is required.")]
    public string ClientId { get; init; } = string.Empty;

    [Required(ErrorMessage = "GoogleOAuth:ClientSecret is required.")]
    public string ClientSecret { get; init; } = string.Empty;

    [Required(ErrorMessage = "GoogleOAuth:RedirectUri is required.")]
    public string RedirectUri { get; init; } = string.Empty;
}

public sealed class AppUrlsOptions
{
    public const string SectionName = "AppUrls";

    [Required(ErrorMessage = "AppUrls:FrontendUrl is required.")]
    public string FrontendUrl { get; init; } = string.Empty;
}