using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Google.Apis.Auth;
using MentalHealthTracker.Api.Core.Configuration;
using MentalHealthTracker.Domain.Errors;
using MentalHealthTracker.Domain.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.Api.Modules.Auth;

public sealed class GoogleOAuthClient
{
    private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";

    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    private const string OpenIdScope = "openid";
    private const string EmailScope = "email";
    private const string ProfileScope = "profile";

    private readonly HttpClient _httpClient;
    private readonly GoogleOAuthOptions _options;
    private readonly Func<string, Task<GoogleJsonWebSignature.Payload>> _idTokenValidator;

    public GoogleOAuthClient(HttpClient httpClient, IOptions<GoogleOAuthOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _idTokenValidator = ValidateIdTokenAsync;
    }

    internal GoogleOAuthClient(
        HttpClient httpClient,
        IOptions<GoogleOAuthOptions> options,
        Func<string, Task<GoogleJsonWebSignature.Payload>> idTokenValidator)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _idTokenValidator = idTokenValidator;
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

    public async Task<GoogleProfile> ExchangeCodeForProfileAsync(string code, CancellationToken cancellationToken = default)
    {
        var tokenResponse = await ExchangeCodeAsync(code, cancellationToken);
        var idToken = tokenResponse.IdToken
            ?? throw new InvalidOperationException("Token endpoint did not return an id_token");

        var validatedPayload = await _idTokenValidator(idToken);
        if (string.IsNullOrEmpty(validatedPayload.Subject))
        {
            throw new UnauthorizedException("Validated id_token is missing the sub claim");
        }

        return new GoogleProfile(
            validatedPayload.Subject,
            validatedPayload.Email,
            validatedPayload.Name,
            validatedPayload.Picture);
    }

    private async Task<TokenResponse> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = _options.RedirectUri,
            ["grant_type"] = "authorization_code",
        });

        using var response = await _httpClient.PostAsync(TokenEndpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Token endpoint returned empty response");
    }

    private Task<GoogleJsonWebSignature.Payload> ValidateIdTokenAsync(string idToken)
    {
        var validationSettings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = [_options.ClientId],
            ForceGoogleCertRefresh = false,
        };

        return GoogleJsonWebSignature.ValidateAsync(idToken, validationSettings);
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("id_token")]
        public string? IdToken { get; init; }
    }
}