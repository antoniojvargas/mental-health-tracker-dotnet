using MentalHealthTracker.Api.Core.Configuration;
using MentalHealthTracker.Api.Core.Errors;
using MentalHealthTracker.Api.Core.Exceptions;
using MentalHealthTracker.Api.Core.Health;
using MentalHealthTracker.Api.Core.Middleware;
using MentalHealthTracker.Api.Core.OpenApi;
using MentalHealthTracker.Api.Core.RateLimiting;
using MentalHealthTracker.Api.Core.Validation;
using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Api.Modules.DailyLog;
using MentalHealthTracker.Api.Modules.Realtime;
using MentalHealthTracker.Infrastructure;
using MentalHealthTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddSecretFileConfiguration();

builder.Host.UseSerilogLogging();

// Apagado ordenado: SIGTERM para de aceptar conexiones nuevas y da margen a
// cerrar las conexiones SignalR y el pool de Postgres, pero si algo se queda
// colgado el host forza la salida al cumplirse los 10 segundos (Kestrel aborta
// entonces cualquier request/WebSocket pendiente).
builder.Host.ConfigureHostOptions(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(10);
});

// Add services to the container.

builder.Services.AddAppOptions(builder.Configuration);
builder.Services.AddReverseProxyTrust();

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddAuthModule();

builder.Services.AddDailyLogModule();
builder.Services.AddRealtimeModule();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<FluentValidationActionFilter>();
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddApiRateLimiting();

const string CorsPolicyName = "Frontend";
var appUrls = builder.Configuration
    .GetSection(AppUrlsOptions.SectionName)
    .Get<AppUrlsOptions>();
builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicyName, policy =>
        policy.WithOrigins(appUrls?.FrontendUrl ?? string.Empty).AllowCredentials()));

builder.Services.AddApiDocumentation();
builder.Services.AddApplicationUptime();

var app = builder.Build();

// Primero de todos: la IP y el esquema reales tienen que estar resueltos antes de que
// el resto del pipeline (rate limiter, logs, redirecciones) los lea. Solo se activa
// detrás de un proxy declarado en ReverseProxy:KnownProxies / KnownNetworks.
if (app.Services.GetRequiredService<IOptions<ReverseProxyOptions>>().Value.Enabled)
{
    app.UseForwardedHeaders();
}

app.UseExceptionHandler();
app.UseRequestId();
app.UseSecurityHeaders();
app.UseCors(CorsPolicyName);

// Configure the HTTP request pipeline.
app.UseApiDocumentation(app.Environment);

app.UseRouting();

app.UseRateLimiter();

app.UseRequireAuth();

app.MapControllers();

app.MapRealtimeHubs();

app.MapAuthEndpoints(app.Environment);

app.MapApiFallback();

app.MapHealthEndpoints();

await InitializeDatabaseAsync(app);

// Modo demo: dotnet run --seed aplica migraciones y carga el usuario demo con 60 días
// de registros (ver DatabaseSeeder). Se resuelve un scope propio y se sale antes de
// arrancar Kestrel.
if (args.Contains("--seed"))
{
    await SeedDatabaseAsync(app);
    return;
}

app.Run();

static async Task SeedDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync();
}

static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer).FullName ?? "DatabaseInitializer");
    await DatabaseInitializer.MigrateAsync(dbContext, logger);
}

// Clase generada por los top-level statements. Se declara pública para que
// WebApplicationFactory<Program> (tests de integración) pueda usarla como entry point.
public partial class Program;
