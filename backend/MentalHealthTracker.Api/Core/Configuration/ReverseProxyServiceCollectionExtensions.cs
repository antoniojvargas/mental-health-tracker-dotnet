using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.Api.Core.Configuration;

public static class ReverseProxyServiceCollectionExtensions
{
    // Requiere AddAppOptions: ReverseProxyOptions se enlaza y valida allí.
    public static IServiceCollection AddReverseProxyTrust(this IServiceCollection services)
    {
        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<ReverseProxyOptions>>((forwarded, proxy) =>
                Apply(forwarded, proxy.Value));

        return services;
    }

    // Traduce las opciones a ForwardedHeadersOptions. Se limpian los valores por defecto
    // (loopback) para que solo cuente lo configurado: confiar además en 127.0.0.1 o ::1
    // sería una confianza que nadie ha declarado.
    public static void Apply(ForwardedHeadersOptions forwarded, ReverseProxyOptions options)
    {
        forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        forwarded.ForwardLimit = options.ForwardLimit;

        forwarded.KnownProxies.Clear();
        forwarded.KnownNetworks.Clear();

        foreach (var proxy in options.KnownProxies)
        {
            forwarded.KnownProxies.Add(IPAddress.Parse(proxy));
        }

        foreach (var network in options.KnownNetworks)
        {
            var parsed = System.Net.IPNetwork.Parse(network);
            forwarded.KnownNetworks.Add(
                new Microsoft.AspNetCore.HttpOverrides.IPNetwork(parsed.BaseAddress, parsed.PrefixLength));
        }
    }
}
