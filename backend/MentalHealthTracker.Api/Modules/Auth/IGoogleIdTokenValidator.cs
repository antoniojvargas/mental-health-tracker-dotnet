using Google.Apis.Auth;

namespace MentalHealthTracker.Api.Modules.Auth;

// Verifica un id_token de Google (firma, emisor, caducidad y audiencia) y devuelve sus
// claims. Es la única parte del flujo OAuth que llama a Google para obtener sus certificados,
// por eso va detrás de una interfaz: los tests del cliente la sustituyen por un doble.
public interface IGoogleIdTokenValidator
{
    Task<GoogleJsonWebSignature.Payload> ValidateAsync(string idToken);
}
