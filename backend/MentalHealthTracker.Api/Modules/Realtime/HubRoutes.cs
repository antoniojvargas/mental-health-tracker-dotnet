namespace MentalHealthTracker.Api.Modules.Realtime;

public static class HubRoutes
{
    // Ruta del hub de registros. El cliente la usa tal cual (frontend/src/services/hub-client.ts,
    // el proxy de nginx y el de Vite reenvían /hub), así que cambiarla rompe el contrato.
    public const string Logs = "/hub/logs";
}
