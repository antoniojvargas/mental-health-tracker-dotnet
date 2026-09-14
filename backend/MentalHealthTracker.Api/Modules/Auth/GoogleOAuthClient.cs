using MentalHealthTracker.Api.Core.Configuration;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.Api.Modules.Auth;

public sealed class GoogleOAuthClient
{
    private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";

    private const string OpenIdScope = "openid";
    private const string EmailScope = "email";
    private const string ProfileScope = "profile";

    private readonly GoogleOAuthOptions _options;

    public GoogleOAuthClient(IOptions<GoogleOAuthOptions> options)
    {
        _options = options.Value;
    }

    public string BuildAuthUrl(string state)
    {
        var scopes = string.Join(" ", [OpenIdScope, EmailScope, ProfileScope]);

        return QueryHelpers.AddQueryString(AuthorizationEndpoint, new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = scopes,
            ["state"] = state,
            ["access_type"] = "online",
            ["prompt"] = "select_account",
        });
    }
}