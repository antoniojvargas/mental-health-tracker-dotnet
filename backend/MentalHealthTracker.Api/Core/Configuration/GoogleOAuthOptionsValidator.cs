using MentalHealthTracker.Api.Modules.Auth;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.Api.Core.Configuration;

// Exige que RedirectUri sea una URL absoluta http/https cuyo path termine en la ruta
// real del callback. Se acepta un prefijo delante (despliegues bajo un subpath) pero no
// una ruta distinta: Google redirige a ese valor y una ruta inexistente rompe el login.
public sealed class GoogleOAuthOptionsValidator : IValidateOptions<GoogleOAuthOptions>
{
    public ValidateOptionsResult Validate(string? name, GoogleOAuthOptions options)
    {
        // Vacío ya lo rechaza [Required]; no se duplica el error.
        if (string.IsNullOrWhiteSpace(options.RedirectUri))
        {
            return ValidateOptionsResult.Success;
        }

        var isValid =
            Uri.TryCreate(options.RedirectUri, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
            uri.AbsolutePath.EndsWith(AuthRoutes.GoogleCallbackPath, StringComparison.Ordinal);

        return isValid
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"GoogleOAuth:RedirectUri must be an absolute http(s) URL whose path ends with " +
                $"'{AuthRoutes.GoogleCallbackPath}'. Received: '{options.RedirectUri}'.");
    }
}
