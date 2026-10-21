using MentalHealthTracker.Api.Core.Hosting;
using MentalHealthTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace MentalHealthTracker.IntegrationTests;

// EF Core, el health check y el cierre ordenado comparten un único NpgsqlDataSource, es decir,
// un único pool. Se observa desde el servidor: cada host de la prueba usa un Application Name
// propio y pg_stat_activity cuenta cuántas conexiones físicas tiene abiertas ese host.
[Collection(IntegrationTestGroup.Name)]
public sealed class NpgsqlDataSourceTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task EfCoreAndTheHealthCheckShareOnePool()
    {
        var (host, applicationName) = CreateHostWithItsOwnApplicationName();
        await using var _ = host;
        var client = host.CreateClient();

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
        }

        var response = await client.GetAsync("/api/health");

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(1, await BackendsOfAsync(applicationName));
    }

    [Fact]
    public async Task GracefulShutdown_ClosesTheIdleConnectionsOfTheSharedPool()
    {
        var (host, applicationName) = CreateHostWithItsOwnApplicationName();
        await using var _ = host;
        var client = host.CreateClient();
        await client.GetAsync("/api/health");
        Assert.Equal(1, await BackendsOfAsync(applicationName));
        var shutdown = host.Services.GetServices<IHostedService>().OfType<GracefulShutdownService>().Single();

        await shutdown.StopAsync(CancellationToken.None);

        // El servidor tarda un instante en retirar el backend cerrado.
        var remaining = -1;
        for (var attempt = 0; attempt < 40 && remaining != 0; attempt++)
        {
            remaining = await BackendsOfAsync(applicationName);
            if (remaining != 0)
            {
                await Task.Delay(50);
            }
        }

        Assert.Equal(0, remaining);
    }

    private (WebApplicationFactory<Program> Host, string ApplicationName) CreateHostWithItsOwnApplicationName()
    {
        var applicationName = $"ds-test-{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(
            Factory.Services.GetRequiredService<IConfiguration>()["Database:ConnectionString"])
        {
            ApplicationName = applicationName,
        }.ToString();

        var host = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = connectionString,
                })));

        return (host, applicationName);
    }

    private async Task<int> BackendsOfAsync(string applicationName)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await dbContext.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE application_name = {applicationName}")
            .SingleAsync();
    }
}
