using System.Net.Http.Headers;
using MentalHealthTracker.Api.Modules.Auth;

namespace MentalHealthTracker.IntegrationTests;

// Único sitio donde los tests construyen la cabecera Cookie de la sesión.
public static class HttpClientExtensions
{
    // Envía una petición con la cookie de sesión del token y desecha el HttpRequestMessage. Es la
    // forma corta de las pruebas que solo necesitan método, ruta, token y, si acaso, un cuerpo.
    public static async Task<HttpResponseMessage> SendAuthenticatedAsync(
        this HttpClient client,
        HttpMethod method,
        string url,
        string token,
        HttpContent? body = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = body };
        request.Headers.AddSessionCookie(token);

        return await client.SendAsync(request);
    }

    // Para las peticiones que ya se montan a mano (cabeceras extra) o los DefaultRequestHeaders
    // de un cliente: añade la cookie `access_token=<token>` sin validar el formato del valor.
    public static void AddSessionCookie(this HttpRequestHeaders headers, string token) =>
        headers.TryAddWithoutValidation("Cookie", $"{SessionCookie.AccessTokenName}={token}");
}
