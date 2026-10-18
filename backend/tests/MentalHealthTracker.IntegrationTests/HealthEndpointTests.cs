using System.Net;
using System.Text.Json;
using MentalHealthTracker.Api.Core.Configuration;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        await using var unreachable = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IOptions<DatabaseOptions>>(Options.Create(new DatabaseOptions
                {
                    ConnectionString = "Host=localhost;Port=1;Database=x;Username=x;Password=x;Timeout=2",
                }))));

        var response = await unreachable.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        Assert.Equal(["status", "db"], root.EnumerateObject().Select(property => property.Name));
        Assert.Equal("error", root.GetProperty("status").GetString());
        Assert.Equal("unreachable", root.GetProperty("db").GetString());
    }
}
