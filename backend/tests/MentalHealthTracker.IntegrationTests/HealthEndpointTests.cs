using System.Net;
using System.Text.Json;
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
}
