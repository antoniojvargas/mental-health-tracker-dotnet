using MentalHealthTracker.Infrastructure;
using MentalHealthTracker.Infrastructure.Persistence;

namespace MentalHealthTracker.Api.Core.Hosting;

public static class DatabaseStartupExtensions
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer).FullName ?? "DatabaseInitializer");
        await DatabaseInitializer.MigrateAsync(dbContext, logger);
    }

    // Modo demo: dotnet run --seed aplica migraciones y carga el usuario demo con 60 días
    // de registros (ver DatabaseSeeder). Se resuelve un scope propio y quien lo llama sale
    // antes de arrancar Kestrel.
    public static async Task SeedDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }
}
