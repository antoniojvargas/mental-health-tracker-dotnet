using System.ComponentModel.DataAnnotations;

namespace MentalHealthTracker.Api.Core.Configuration;

// Confianza en el proxy inverso que termina TLS y reenvía el tráfico (nginx en producción).
// Desactivado por defecto: en desarrollo y tests el cliente conecta directo a Kestrel y
// RemoteIpAddress ya es la IP real.
public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    public bool Enabled { get; init; }

    // IPs individuales de proxies de confianza.
    public string[] KnownProxies { get; init; } = [];

    // Redes de proxies de confianza en notación CIDR (p. ej. la subred de Docker).
    public string[] KnownNetworks { get; init; } = [];

    // Cuántas entradas de X-Forwarded-* se leen, empezando por la más a la derecha (la que
    // añade el proxy de confianza). Con 1, un cliente no puede inyectar otra IP.
    [Range(1, 10, ErrorMessage = "ReverseProxy:ForwardLimit must be between 1 and 10.")]
    public int ForwardLimit { get; init; } = 1;
}
