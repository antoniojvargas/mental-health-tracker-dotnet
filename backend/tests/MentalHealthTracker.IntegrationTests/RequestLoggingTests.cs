using System.Globalization;
using System.Net;
using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Api.Modules.DailyLog;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace MentalHealthTracker.IntegrationTests;

// El registro de peticiones: una línea por petición con RequestId y la IP del cliente, nivel según
// el status, sin /api/health y sin la query string. Cada test usa un host con su propio logger de
// Serilog que guarda los eventos en memoria.
[Collection(IntegrationTestGroup.Name)]
public sealed class RequestLoggingTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const string RequestLogSource = "Serilog.AspNetCore.RequestLoggingMiddleware";

    [Fact]
    public async Task OkRequest_IsLoggedAsInformationWithItsRequestIdAndClientIp()
    {
        var (_, _, token) = await CreateAuthenticatedSessionAsync($"log-{Guid.NewGuid():N}@example.com");
        var sink = new CollectingSink();
        await using var host = HostWith(sink);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/logs");
        request.Headers.TryAddWithoutValidation("Cookie", $"{SessionCookie.AccessTokenName}={token}");
        request.Headers.TryAddWithoutValidation("X-Request-Id", "trace-ok-1");

        using var response = await host.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entry = Assert.Single(sink.RequestLogs);
        Assert.Equal(LogEventLevel.Information, entry.Level);
        Assert.Equal("trace-ok-1", Property(entry, "RequestId"));
        Assert.Equal("GET", Property(entry, "RequestMethod"));
        Assert.Equal("/api/logs", Property(entry, "RequestPath"));
        Assert.Equal("200", Property(entry, "StatusCode"));
        Assert.True(entry.Properties.ContainsKey("ClientIp"));
    }

    [Fact]
    public async Task NotFound_IsLoggedAsWarningWithTheGeneratedRequestIdOfTheResponse()
    {
        var sink = new CollectingSink();
        await using var host = HostWith(sink);

        using var response = await host.CreateClient().GetAsync("/api/does-not-exist");

        var entry = Assert.Single(sink.RequestLogs);
        Assert.Equal(LogEventLevel.Warning, entry.Level);
        Assert.Equal("404", Property(entry, "StatusCode"));
        Assert.Equal(response.Headers.GetValues("X-Request-Id").Single(), Property(entry, "RequestId"));
    }

    [Fact]
    public async Task Unauthorized_IsLoggedAsWarning()
    {
        var sink = new CollectingSink();
        await using var host = HostWith(sink);

        using var response = await host.CreateClient().GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var entry = Assert.Single(sink.RequestLogs);
        Assert.Equal(LogEventLevel.Warning, entry.Level);
        Assert.Equal("401", Property(entry, "StatusCode"));
    }

    [Fact]
    public async Task ServerError_IsLoggedAsErrorWithTheFinalStatusAndNoDuplicatedException()
    {
        var (_, _, token) = await CreateAuthenticatedSessionAsync($"log-{Guid.NewGuid():N}@example.com");
        var sink = new CollectingSink();
        await using var host = HostWith(sink, services =>
            services.AddScoped<IDailyLogService, FailingDailyLogService>());
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/logs");
        request.Headers.TryAddWithoutValidation("Cookie", $"{SessionCookie.AccessTokenName}={token}");

        using var response = await host.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var entry = Assert.Single(sink.RequestLogs);
        Assert.Equal(LogEventLevel.Error, entry.Level);
        Assert.Equal("500", Property(entry, "StatusCode"));
        Assert.Null(entry.Exception);
    }

    [Fact]
    public async Task Health_IsNeverLogged_AndDoesNotHideOtherRequests()
    {
        var sink = new CollectingSink();
        await using var host = HostWith(sink);
        var client = host.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            using var health = await client.GetAsync("/api/health");
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        }

        using var other = await client.GetAsync("/api/does-not-exist");

        var entry = Assert.Single(sink.RequestLogs);
        Assert.Equal("/api/does-not-exist", Property(entry, "RequestPath"));
    }

    [Fact]
    public async Task TheQueryStringNeverReachesTheLog()
    {
        var sink = new CollectingSink();
        await using var host = HostWith(sink);
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/api/auth/google/callback?state=abc&code=SUPER-SECRET-CODE");

        var entry = Assert.Single(sink.RequestLogs);
        Assert.Equal("/api/auth/google/callback", Property(entry, "RequestPath"));
        Assert.DoesNotContain("SUPER-SECRET-CODE", entry.RenderMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.DoesNotContain(entry.Properties.Values, value => value.ToString().Contains("SUPER-SECRET-CODE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BehindATrustedProxy_TheLoggedClientIpIsTheForwardedOne()
    {
        var sink = new CollectingSink();
        await using var host = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ReverseProxy:Enabled"] = "true",
                    ["ReverseProxy:KnownProxies:0"] = ForwardedHeadersFixture.TrustedProxy,
                }));
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<Serilog.ILogger>(CapturingLogger(sink));
                services.AddSingleton<Microsoft.AspNetCore.Hosting.IStartupFilter, TestRemoteIpStartupFilter>();
            });
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/does-not-exist");
        request.Headers.Add(TestRemoteIpStartupFilter.HeaderName, ForwardedHeadersFixture.TrustedProxy);
        request.Headers.Add("X-Forwarded-For", "203.0.113.77");

        using var response = await host.CreateClient().SendAsync(request);

        Assert.Equal("203.0.113.77", Property(Assert.Single(sink.RequestLogs), "ClientIp"));
    }

    private WebApplicationFactory<Program> HostWith(CollectingSink sink, Action<IServiceCollection>? configure = null) =>
        Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<Serilog.ILogger>(CapturingLogger(sink));
                configure?.Invoke(services);
            }));

    private static Logger CapturingLogger(CollectingSink sink) =>
        new Serilog.LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();

    private static string Property(LogEvent entry, string name) =>
        entry.Properties[name].ToString().Trim('"');

    private sealed class CollectingSink : ILogEventSink
    {
        private readonly List<LogEvent> _events = [];

        public IReadOnlyList<LogEvent> RequestLogs
        {
            get
            {
                lock (_events)
                {
                    return _events
                        .Where(e => e.Properties.TryGetValue("SourceContext", out var source)
                            && source.ToString().Trim('"') == RequestLogSource)
                        .ToList();
                }
            }
        }

        public void Emit(LogEvent logEvent)
        {
            lock (_events)
            {
                _events.Add(logEvent);
            }
        }
    }

    private sealed class FailingDailyLogService : IDailyLogService
    {
        public Task<(MentalHealthTracker.Api.Modules.DailyLog.Dtos.DailyLogResponse Response, bool Created)> UpsertAsync(
            Guid userId, MentalHealthTracker.Api.Modules.DailyLog.Dtos.CreateDailyLogRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");

        public Task<MentalHealthTracker.Api.Modules.DailyLog.Dtos.DailyLogListResponse> ListAsync(
            Guid userId, MentalHealthTracker.Api.Modules.DailyLog.Dtos.ListDailyLogsQuery query, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");

        public Task<MentalHealthTracker.Api.Modules.DailyLog.Dtos.DailyLogResponse?> GetTodayAsync(
            Guid userId, DateOnly? logDate, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");
    }
}
