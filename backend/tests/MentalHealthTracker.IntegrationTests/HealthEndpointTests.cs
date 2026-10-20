using System.Diagnostics;
using System.Net;
using System.Text.Json;
using MentalHealthTracker.Api.Core.Health;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace MentalHealthTracker.IntegrationTests;

[Collection(IntegrationTestGroup.Name)]
public sealed class HealthEndpointTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory), IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Health_WithReachableDatabase_Returns200WithStatusAndUptime()
    {
        var response = await Factory.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        Assert.Equal(["status", "uptime"], root.EnumerateObject().Select(property => property.Name));
        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.True(root.GetProperty("uptime").TryGetInt32(out var uptime));
        Assert.True(uptime >= 0);
    }

    [Fact]
    public async Task Health_WithUnreachableDatabase_Returns503WithErrorBody()
    {
        // Host propio (su propio NpgsqlDataSource) cuya fuente de conexiones deja de servirlas:
        // el health check no puede abrir conexión y debe contestar 503, no propagar el error.
        await using var unreachable = Factory.WithWebHostBuilder(_ => { });
        var client = unreachable.CreateClient();
        await unreachable.Services.GetRequiredService<NpgsqlDataSource>().DisposeAsync();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        Assert.Equal(["status", "db"], root.EnumerateObject().Select(property => property.Name));
        Assert.Equal("error", root.GetProperty("status").GetString());
        Assert.Equal("unreachable", root.GetProperty("db").GetString());
    }

    [Fact]
    public async Task Health_WithAHungDatabase_AnswersTheSame503WithinTheTimeoutInsteadOfHanging()
    {
        var probe = new HangingProbe();
        await using var hung = HostWith(probe);
        var client = hung.CreateClient();

        var watch = Stopwatch.StartNew();
        var response = await client.GetAsync("/api/health").WaitAsync(TimeSpan.FromSeconds(10));
        watch.Stop();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(["status", "db"], body.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal("error", body.RootElement.GetProperty("status").GetString());
        Assert.Equal("unreachable", body.RootElement.GetProperty("db").GetString());
        Assert.True(probe.WasCancelled, "La sonda debería haber recibido la cancelación del timeout.");
        Assert.InRange(watch.Elapsed, TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Health_WhenTheClientAbortsWhileTheDatabaseHangs_StopsProbingImmediately()
    {
        var probe = new HangingProbe();
        await using var hung = HostWith(probe);
        var client = hung.CreateClient();
        using var abort = new CancellationTokenSource();

        var request = client.GetAsync("/api/health", abort.Token);
        await probe.Started.WaitAsync(TimeSpan.FromSeconds(5));
        await abort.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        await probe.Cancelled.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task TheProbeCommand_HasItsOwnTimeout_SoASlowQueryDoesNotWaitForeverEvenWithoutAToken()
    {
        var dataSource = Factory.Services.GetRequiredService<NpgsqlDataSource>();
        var probe = new NpgsqlDatabaseProbe(dataSource, "SELECT pg_sleep(30)");

        var watch = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<Exception>(() => probe.ProbeAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(15)));
        watch.Stop();

        Assert.InRange(watch.Elapsed, TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(8));
    }

    [Fact]
    public async Task TheDefaultProbe_RunsSelectOneAgainstTheSharedDataSource()
    {
        var dataSource = Factory.Services.GetRequiredService<NpgsqlDataSource>();

        await new NpgsqlDatabaseProbe(dataSource).ProbeAsync(CancellationToken.None);
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> HostWith(IDatabaseProbe probe) =>
        Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton(probe)));

    // Una "base de datos colgada": la sonda no termina hasta que la cancelan.
    private sealed class HangingProbe : IDatabaseProbe
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource _cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public Task Cancelled => _cancelled.Task;

        public bool WasCancelled => _cancelled.Task.IsCompleted;

        public async Task ProbeAsync(CancellationToken cancellationToken)
        {
            _started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _cancelled.TrySetResult();
                throw;
            }
        }
    }
}
