using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace MentalHealthTracker.IntegrationTests;

// Database:MigrateOnStartup decide si el arranque toca el esquema. Se prueba contra una base
// recién creada y vacía (la compartida ya está migrada y no distinguiría nada): con el valor
// por defecto el host crea el historial de migraciones; con false lo deja intacto, que es lo
// que permite arrancar réplicas contra un esquema migrado antes por el job --migrate.
[Collection(IntegrationTestGroup.Name)]
public sealed class MigrateOnStartupTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task ByDefault_StartingTheHostMigratesAnEmptyDatabase()
    {
        await using var scratch = await ScratchDatabase.CreateAsync();

        await using (var host = HostAgainst(factory, scratch.ConnectionString, migrateOnStartup: null))
        {
            host.CreateClient();
        }

        Assert.True(await scratch.HasMigrationHistoryAsync());
    }

    [Fact]
    public async Task WithMigrateOnStartupFalse_StartingTheHostLeavesTheDatabaseUntouched()
    {
        await using var scratch = await ScratchDatabase.CreateAsync();

        await using (var host = HostAgainst(factory, scratch.ConnectionString, migrateOnStartup: false))
        {
            host.CreateClient();
        }

        Assert.False(await scratch.HasMigrationHistoryAsync());
    }

    private static WebApplicationFactory<Program> HostAgainst(
        CustomWebApplicationFactory factory, string connectionString, bool? migrateOnStartup) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var settings = new Dictionary<string, string?> { ["Database:ConnectionString"] = connectionString };
                if (migrateOnStartup is { } value)
                {
                    settings["Database:MigrateOnStartup"] = value ? "true" : "false";
                }

                configuration.AddInMemoryCollection(settings);
            }));

    // Base temporal con nombre único en el mismo servidor que la de test; se borra al terminar.
    private sealed class ScratchDatabase : IAsyncDisposable
    {
        private readonly string _adminConnectionString;
        private readonly string _name;

        private ScratchDatabase(string adminConnectionString, string name, string connectionString)
        {
            _adminConnectionString = adminConnectionString;
            _name = name;
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }

        public static async Task<ScratchDatabase> CreateAsync()
        {
            var name = $"mht_scratch_{Guid.NewGuid():N}";
            var admin = CustomWebApplicationFactory.ConnectionString;
            var scratch = new NpgsqlConnectionStringBuilder(admin) { Database = name, Pooling = false }.ConnectionString;

            await using var connection = new NpgsqlConnection(admin);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
            await command.ExecuteNonQueryAsync();

            return new ScratchDatabase(admin, name, scratch);
        }

        public async Task<bool> HasMigrationHistoryAsync()
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL", connection);
            return (bool)(await command.ExecuteScalarAsync())!;
        }

        public async ValueTask DisposeAsync()
        {
            await using var connection = new NpgsqlConnection(_adminConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
