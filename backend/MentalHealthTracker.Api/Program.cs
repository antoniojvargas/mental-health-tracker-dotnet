using FluentValidation;
using MentalHealthTracker.Api.Core.Configuration;
using MentalHealthTracker.Api.Core.Exceptions;
using MentalHealthTracker.Api.Core.Middleware;
using MentalHealthTracker.Api.Core.Validation;
using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Api.Modules.DailyLog;
using MentalHealthTracker.Api.Modules.DailyLog.Dtos;
using MentalHealthTracker.Api.Modules.DailyLog.Validators;
using MentalHealthTracker.Domain.Repositories;
using MentalHealthTracker.Infrastructure.Persistence;
using MentalHealthTracker.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddSecretFileConfiguration();

builder.Host.UseSerilogLogging();

// Add services to the container.

builder.Services.AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<GoogleOAuthOptions>()
    .Bind(builder.Configuration.GetSection(GoogleOAuthOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AppUrlsOptions>()
    .Bind(builder.Configuration.GetSection(AppUrlsOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddScoped<DatabaseSeeder>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddSingleton<JwtService>();
builder.Services.AddSingleton<AuthCookieOptions>();
builder.Services.AddHttpClient<GoogleOAuthClient>();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration
        .GetSection(DatabaseOptions.SectionName)
        .Get<DatabaseOptions>()?.ConnectionString;
    options.UseNpgsql(connectionString);
});

builder.Services.AddScoped<IValidator<CreateDailyLogRequest>, CreateDailyLogValidator>();
builder.Services.AddScoped<IValidator<ListDailyLogsQuery>, ListDailyLogsQueryValidator>();
builder.Services.AddScoped<IDailyLogService, DailyLogService>();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<FluentValidationActionFilter>();
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

const string CorsPolicyName = "Frontend";
var appUrls = builder.Configuration
    .GetSection(AppUrlsOptions.SectionName)
    .Get<AppUrlsOptions>();
builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicyName, policy =>
        policy.WithOrigins(appUrls?.FrontendUrl ?? string.Empty).AllowCredentials()));

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseRequestId();
app.UseSecurityHeaders();
app.UseCors(CorsPolicyName);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseRequireAuth();

app.MapControllers();

if (app.Environment.IsEnvironment("Testing"))
{
    app.MapTestAuthEndpoints();
}

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
        logger.LogError(exception, "Database health check failed");
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
public partial class Program { }
