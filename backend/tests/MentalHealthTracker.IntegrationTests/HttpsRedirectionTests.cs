using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MentalHealthTracker.IntegrationTests;

// El contenedor de la API solo sirve HTTP: el TLS (y HSTS) terminan en el proxy de borde.
// Una redirección a HTTPS desde la API sería incorrecta (no hay puerto HTTPS al que
// redirigir) y un 307 sobre un POST no aporta nada. Se fuerza HTTPS_PORT para que, si
// alguien volviera a añadir UseHttpsRedirection, la redirección ocurriera de verdad en
// el TestServer y este test fallara; sin ese puerto el middleware solo avisa en el log.
[Collection(IntegrationTestGroup.Name)]
public sealed class HttpsRedirectionTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Health_OverHttp_RespondsOkWithoutRedirect()
    {
        using var httpsConfigured = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["HTTPS_PORT"] = "443",
                })));
        var client = httpsConfigured.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost"),
        });

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }
}
