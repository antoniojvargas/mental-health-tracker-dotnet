namespace MentalHealthTracker.Api.Modules.Realtime;

// Nombres de los eventos que el hub de registros emite al cliente. Son parte del contrato:
// el frontend se suscribe a estas cadenas (frontend/src/services/hub-client.ts).
public static class LogHubEvents
{
    public const string LogCreated = "log:created";

    public const string LogUpdated = "log:updated";
}
