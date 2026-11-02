using MentalHealthTracker.Api.Core.Configuration;
using MentalHealthTracker.Api.Core.Logging;
using MentalHealthTracker.Infrastructure;
using MentalHealthTracker.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.Api.Core.Hosting;

public static partial class DatabaseStartupExtensions
{
    public const string MigrateFlag = "--migrate";

    public const string SeedFlag = "--seed";

    // Modo job de despliegue: dotnet MentalHealthTracker.Api.dll --migrate aplica las migraciones y
    // termina sin arrancar Kestrel (igual que --seed).
    public static bool IsMigrateOnly(string[] args) => args.Contains(MigrateFlag);

    // El arranque migra salvo que Database:MigrateOnStartup lo apague. --migrate y --seed migran
    // siempre: el primero es justo para eso y el segundo necesita el esquema para sembrar.
    public static bool ShouldMigrate(bool migrateOnStartup, string[] args) =>
        migrateOnStartup || args.Contains(MigrateFlag) || args.Contains(SeedFlag);

    public static async Task MigrateIfNeededAsync(this WebApplication app, string[] args)
    {
        var options = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        if (ShouldMigrate(options.MigrateOnStartup, args))
        {
            await app.InitializeDatabaseAsync();
            return;
        }

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseStartupExtensions).FullName ?? "DatabaseStartupExtensions");
        LogMigrationsSkipped(logger);
    }

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

    [LoggerMessage(EventId = ApiLogEvents.MigrationsSkipped, EventName = nameof(ApiLogEvents.MigrationsSkipped), Level = LogLevel.Information, Message = "Database migrations skipped at startup (Database:MigrateOnStartup=false); run the --migrate job before deploying")]
    private static partial void LogMigrationsSkipped(ILogger logger);
}
