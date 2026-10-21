using System.Net;
using MentalHealthTracker.Api.Core.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.IntegrationTests;

// Detrás de nginx, RemoteIpAddress es la IP del proxy y la cuota por IP de la política
// "auth" se compartiría entre todos los usuarios. Con ReverseProxy:Enabled el backend
// resuelve la IP real desde X-Forwarded-For, pero solo si la petición llega de un proxy
// declarado como conocido. TestServer no tiene socket, así que un IStartupFilter de test
// fija Connection.RemoteIpAddress (cabecera X-Test-Remote-Ip) antes de ForwardedHeaders.
[Collection(IntegrationTestGroup.IsolatedName)]
public sealed class ForwardedHeadersTests(ForwardedHeadersFixture fixture)
    : IClassFixture<ForwardedHeadersFixture>
{
    private int AuthPermitLimit => fixture.RateLimiting.Auth.PermitLimit;
    private const string AuthCallbackUrl = "/api/auth/google/callback?state=x&code=y";

    private static readonly string TrustedProxy = ForwardedHeadersFixture.TrustedProxy;
    // Una dirección distinta por test: los tests comparten host y, por tanto, contadores.
    private const string UntrustedAddress = "198.51.100.7";
    private const string OtherUntrustedAddress = "198.51.100.8";

    [Fact]
    public async Task AuthPolicy_BehindTrustedProxy_PartitionsByForwardedClientIp()
    {
        var client = fixture.CreateClient();

        for (var i = 0; i < AuthPermitLimit; i++)
        {
            var response = await Send(client, TrustedProxy, forwardedFor: "203.0.113.1");
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var exhausted = await Send(client, TrustedProxy, forwardedFor: "203.0.113.1");
        var otherClient = await Send(client, TrustedProxy, forwardedFor: "203.0.113.2");

        Assert.Equal(HttpStatusCode.TooManyRequests, exhausted.StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, otherClient.StatusCode);
    }

    [Fact]
    public async Task AuthPolicy_FromUntrustedAddress_IgnoresForwardedForAndCannotBypassQuota()
    {
        var client = fixture.CreateClient();

        for (var i = 0; i < AuthPermitLimit; i++)
        {
            var response = await Send(client, UntrustedAddress, forwardedFor: "203.0.113.10");
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var withAnotherForwardedIp = await Send(client, UntrustedAddress, forwardedFor: "203.0.113.11");

        Assert.Equal(HttpStatusCode.TooManyRequests, withAnotherForwardedIp.StatusCode);
    }

    [Fact]
    public async Task ForwardedProto_FromTrustedProxy_SetsRequestSchemeToHttps()
    {
        var client = fixture.CreateClient();

        await Send(client, TrustedProxy, forwardedFor: "203.0.113.20", forwardedProto: "https");

        Assert.Equal("https", fixture.Observations.LastScheme);
    }

    [Fact]
    public async Task ForwardedProto_FromUntrustedAddress_KeepsOriginalScheme()
    {
        var client = fixture.CreateClient();

        await Send(client, OtherUntrustedAddress, forwardedFor: "203.0.113.21", forwardedProto: "https");

        Assert.Equal("http", fixture.Observations.LastScheme);
    }

    private static Task<HttpResponseMessage> Send(
        HttpClient client,
        string remoteAddress,
        string forwardedFor,
        string? forwardedProto = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, AuthCallbackUrl);
        request.Headers.Add(TestRemoteIpStartupFilter.HeaderName, remoteAddress);
        request.Headers.Add("X-Forwarded-For", forwardedFor);

        if (forwardedProto is not null)
        {
            request.Headers.Add("X-Forwarded-Proto", forwardedProto);
        }

        return client.SendAsync(request);
    }
}

// Host propio (derivado de CustomWebApplicationFactory) con el proxy de confianza activado.
// Al ser un host distinto, el estado del rate limiter queda aislado del resto de tests.
public sealed class ForwardedHeadersFixture : IDisposable
{
    public const string TrustedProxy = "10.0.0.2";

    private readonly CustomWebApplicationFactory _baseFactory = new();
    private readonly WebApplicationFactory<Program> _factory;

    public ForwardedHeadersFixture()
    {
        _factory = _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ReverseProxy:Enabled"] = "true",
                    ["ReverseProxy:KnownProxies:0"] = TrustedProxy,
                }));

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(Observations);
                services.AddSingleton<IStartupFilter, TestRemoteIpStartupFilter>();
                services.Configure<MvcOptions>(options => options.Filters.Add<ObserveRequestFilter>());
            });
        });
    }

    public RequestObservations Observations { get; } = new();

    public RateLimitingOptions RateLimiting =>
        _factory.Services.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

    public HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public void Dispose()
    {
        _factory.Dispose();
        _baseFactory.Dispose();
    }
}

public sealed class RequestObservations
{
    public string? LastScheme { get; set; }
}

// Se ejecuta dentro de MVC, es decir, después de todo el middleware: registra lo que la
// aplicación ve realmente una vez resueltas las cabeceras reenviadas.
public sealed class ObserveRequestFilter(RequestObservations observations) : IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context) =>
        observations.LastScheme = context.HttpContext.Request.Scheme;

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}

// Va registrado antes que cualquier middleware de la app, así que fija la IP de origen
// antes de que ForwardedHeaders la evalúe.
public sealed class TestRemoteIpStartupFilter : IStartupFilter
{
    public const string HeaderName = "X-Test-Remote-Ip";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            if (context.Request.Headers.TryGetValue(HeaderName, out var value) &&
                IPAddress.TryParse(value, out var address))
            {
                context.Connection.RemoteIpAddress = address;
            }

            await nextMiddleware(context);
        });

        next(app);
    };
}
