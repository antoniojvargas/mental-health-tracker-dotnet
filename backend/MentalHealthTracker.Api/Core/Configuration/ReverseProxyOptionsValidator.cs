using System.Net;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.Api.Core.Configuration;

// Las IPs y los CIDR deben parsear, y con Enabled=true debe haber al menos un proxy o red
// de confianza: sin ninguno, cualquier origen podría falsificar X-Forwarded-For y saltarse
// los límites por IP.
public sealed class ReverseProxyOptionsValidator : IValidateOptions<ReverseProxyOptions>
{
    public ValidateOptionsResult Validate(string? name, ReverseProxyOptions options)
    {
        var failures = new List<string>();

        foreach (var proxy in options.KnownProxies)
        {
            if (!IPAddress.TryParse(proxy, out _))
            {
                failures.Add($"ReverseProxy:KnownProxies contains an invalid IP address: '{proxy}'.");
            }
        }

        foreach (var network in options.KnownNetworks)
        {
            if (!IPNetwork.TryParse(network, out _))
            {
                failures.Add(
                    $"ReverseProxy:KnownNetworks contains an invalid CIDR: '{network}' " +
                    "(expected a network address with prefix length, e.g. '172.28.0.0/24').");
            }
        }

        if (options.Enabled && options.KnownProxies.Length == 0 && options.KnownNetworks.Length == 0)
        {
            failures.Add(
                "ReverseProxy:Enabled is true but neither ReverseProxy:KnownProxies nor " +
                "ReverseProxy:KnownNetworks has entries. Trusting any origin would allow spoofing the client IP.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
