using Google.Apis.Auth;
using MentalHealthTracker.Api.Core.Configuration;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.Api.Modules.Auth;

// Implementación por defecto: valida con GoogleJsonWebSignature exigiendo que la audiencia
// sea el ClientId de esta aplicación.
public sealed class GoogleIdTokenValidator(IOptions<GoogleOAuthOptions> options) : IGoogleIdTokenValidator
{
    public Task<GoogleJsonWebSignature.Payload> ValidateAsync(string idToken)
    {
        var validationSettings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = [options.Value.ClientId],
            ForceGoogleCertRefresh = false,
        };

        return GoogleJsonWebSignature.ValidateAsync(idToken, validationSettings);
    }
}
