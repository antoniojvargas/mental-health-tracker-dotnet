using MentalHealthTracker.Api.Core.Configuration;
using MentalHealthTracker.Api.Core.Exceptions;
using MentalHealthTracker.Api.Core.Middleware;
using MentalHealthTracker.Api.Core.OpenApi;
using MentalHealthTracker.Api.Core.RateLimiting;
using MentalHealthTracker.Api.Core.Validation;
using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Api.Modules.DailyLog;
using MentalHealthTracker.Api.Modules.Realtime;
using MentalHealthTracker.Domain.Errors;
using MentalHealthTracker.Infrastructure;
using MentalHealthTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

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

// Cualquier ruta de /api sin endpoint cae aquí y sale por el manejador global con el
// mismo sobre de error que el resto de la API, en lugar de un 404 vacío. Solo cubre
// /api: /hub y Swagger quedan fuera. Es el endpoint de menor prioridad, así que no
// tapa a los reales; por contra, un método no admitido en una ruta existente también
// termina aquí (404 en vez de 405).
app.MapFallback("/api/{**path}", (HttpContext context) =>
    throw new NotFoundException($"Route {context.Request.Path} does not exist"));

var startedAt = DateTimeOffset.UtcNow;

// Health check con prueba real de conectividad: devolver un literal "ok" fijo no basta
// porque los orquestadores (Docker healthcheck, load balancers) usan este endpoint para
// decidir si el contenedor está sano. Un 200 constante enmascara caídas de la base de
// datos: no se reiniciaría el contenedor, no habría alertas y los clientes fallarían
// después con errores confusos. Solo un SELECT 1 real refleja la disponibilidad real.
app.MapGet("/api/health", async (
    IOptions<DatabaseOptions> databaseOptions,
    ILogger<Program> logger) =>
{
    var uptime = DateTimeOffset.UtcNow - startedAt;

    try
    {
        await using var connection = new NpgsqlConnection(databaseOptions.Value.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        await command.ExecuteScalarAsync();
    }
    catch (Exception exception)
    {
        Program.LogHealthCheckFailed(logger, exception);
        return Results.Json(
            new { status = "error", db = "unreachable" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Json(
        new { status = "ok", uptime = (int)uptime.TotalSeconds });
});

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
public partial class Program
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Database health check failed")]
    private static partial void LogHealthCheckFailed(ILogger logger, Exception exception);
}
