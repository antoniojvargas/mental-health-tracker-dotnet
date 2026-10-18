using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace MentalHealthTracker.Api.Modules.Auth;

// El state anti login-CSRF del flujo OAuth: se genera al iniciar el login, viaja en una
// cookie httpOnly y en la URL de Google, y el callback exige que ambos coincidan. Sin la
// cookie que fijó el propio backend, una navegación de vuelta forjada se rechaza.
public sealed class OAuthStateService(AuthCookieOptions cookieOptions)
{
    private const int StateBytes = 32;

    private static readonly TimeSpan CookieMaxAge = TimeSpan.FromMinutes(10);

    // Genera un state aleatorio, lo escribe en la cookie y lo devuelve para la URL de Google.
    public string Issue(HttpResponse response)
    {
        var state = GenerateState();

        response.Cookies.Append(
            SessionCookie.OAuthStateName,
            state,
            cookieOptions.CreateWithMaxAge(CookieMaxAge));

        return state;
    }

    // Borra la cookie ANTES de comparar (es de un solo uso, también si la comparación falla)
    // y devuelve si el state recibido coincide con el que contenía.
    public bool Consume(HttpContext context, string? receivedState)
    {
        var expectedState = context.Request.Cookies[SessionCookie.OAuthStateName];
        context.Response.Cookies.Delete(SessionCookie.OAuthStateName, cookieOptions.Create());

        return Matches(expectedState, receivedState);
    }

    // Comparación en tiempo constante: no filtra por el tiempo de respuesta cuánto del state
    // coincide. La longitud sí se compara antes, y no es un secreto (siempre es la misma).
    public static bool Matches(string? expectedState, string? receivedState)
    {
        if (expectedState is null || receivedState is null ||
            expectedState.Length != receivedState.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedState),
            Encoding.UTF8.GetBytes(receivedState));
    }

    private static string GenerateState()
    {
        Span<byte> bytes = stackalloc byte[StateBytes];
        RandomNumberGenerator.Fill(bytes);
        return WebEncoders.Base64UrlEncode(bytes);
    }
}
