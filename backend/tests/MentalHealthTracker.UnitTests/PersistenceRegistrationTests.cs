using MentalHealthTracker.Domain.Repositories;
using MentalHealthTracker.Infrastructure;
using MentalHealthTracker.Infrastructure.Persistence;
using MentalHealthTracker.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MentalHealthTracker.UnitTests;

// AddPersistence es lo que Infrastructure aporta al contenedor. Resolver los servicios no
// abre ninguna conexión: el DbContext solo guarda la cadena.
public sealed class PersistenceRegistrationTests
{
    private const string ConnectionString = "Host=localhost;Port=5432;Database=persistence;Username=u;Password=p";

    private static ServiceProvider Build(IConfiguration configuration) =>
        new ServiceCollection()
            .AddLogging()
            .AddSingleton(TimeProvider.System)
            .AddSingleton(NullLogger<DatabaseSeeder>.Instance)
            .AddPersistence(configuration)
            .BuildServiceProvider(validateScopes: true);

    private static IConfigurationRoot ConfigurationWith(string? connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Database:ConnectionString"] = connectionString })
            .Build();

    [Fact]
    public void RegistersTheRepositoriesTheSeederAndTheDbContextAsScoped()
    {
        using var provider = Build(ConfigurationWith(ConnectionString));
        using var scope = provider.CreateScope();

        Assert.IsType<UserRepository>(scope.ServiceProvider.GetRequiredService<IUserRepository>());
        Assert.IsType<DailyLogRepository>(scope.ServiceProvider.GetRequiredService<IDailyLogRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<DatabaseSeeder>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Fact]
    public void ScopedServicesAreSharedInAScopeAndDifferentBetweenScopes()
    {
        using var provider = Build(ConfigurationWith(ConnectionString));
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        Assert.Same(
            first.ServiceProvider.GetRequiredService<AppDbContext>(),
            first.ServiceProvider.GetRequiredService<AppDbContext>());
        Assert.NotSame(
            first.ServiceProvider.GetRequiredService<AppDbContext>(),
            second.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Fact]
    public void DbContextUsesTheConfiguredConnectionString()
    {
        using var provider = Build(ConfigurationWith(ConnectionString));
        using var scope = provider.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        AssertPointsAtTheConfiguredDatabase(dbContext.Database.GetConnectionString());
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", dbContext.Database.ProviderName);
    }

    [Fact]
    public void TheConnectionStringIsReadWhenTheContextIsCreatedNotWhenRegistered()
    {
        // Un proveedor añadido DESPUÉS de registrar cuenta, como hacen los tests de
        // integración con su factory: la configuración se lee al crear el contexto.
        var mutable = new ConfigurationManager();
        using var provider = Build(mutable);

        mutable.AddInMemoryCollection(new Dictionary<string, string?> { ["Database:ConnectionString"] = ConnectionString });
        using var scope = provider.CreateScope();

        AssertPointsAtTheConfiguredDatabase(
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetConnectionString());
    }

    // Con un NpgsqlDataSource la cadena que expone EF no incluye la contraseña.
    private static void AssertPointsAtTheConfiguredDatabase(string? connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        Assert.Equal("localhost", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("persistence", builder.Database);
        Assert.Equal("u", builder.Username);
    }

    [Fact]
    public void ThereIsOneSharedNpgsqlDataSourceAcrossScopes()
    {
        using var provider = Build(ConfigurationWith(ConnectionString));
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        var dataSource = provider.GetRequiredService<NpgsqlDataSource>();

        Assert.Same(dataSource, second.ServiceProvider.GetRequiredService<NpgsqlDataSource>());
        Assert.Same(dataSource, first.ServiceProvider.GetRequiredService<NpgsqlDataSource>());
    }

    [Fact]
    public void TheDataSourceIsDisposedWithTheContainer()
    {
        var provider = Build(ConfigurationWith(ConnectionString));
        var dataSource = provider.GetRequiredService<NpgsqlDataSource>();

        provider.Dispose();

        Assert.ThrowsAny<ObjectDisposedException>(() => dataSource.CreateConnection().Open());
    }
}
