using MentalHealthTracker.Api.Core.Configuration;
using MentalHealthTracker.Api.Core.Errors;
using MentalHealthTracker.Api.Core.Health;
using MentalHealthTracker.Api.Core.Hosting;
using MentalHealthTracker.Api.Core.OpenApi;
using MentalHealthTracker.Api.Core.RateLimiting;
using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Api.Modules.DailyLog;
using MentalHealthTracker.Api.Modules.Realtime;
using MentalHealthTracker.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddSecretFileConfiguration();
builder.Host.UseSerilogLogging().UseGracefulShutdownTimeout();

builder.Services.AddAppOptions(builder.Configuration).AddReverseProxyTrust();
builder.Services.AddPersistence(builder.Configuration).AddApiCore().AddFrontendCors(builder.Configuration);
builder.Services.AddAuthModule().AddDailyLogModule().AddRealtimeModule();
builder.Services.AddApiRateLimiting().AddApiDocumentation().AddApplicationHealth();

var app = builder.Build();
app.UseApiPipeline();
app.MapControllers();
app.MapRealtimeHubs();
app.MapAuthEndpoints(app.Environment);
app.MapApiFallback();
app.MapHealthEndpoints();
await app.InitializeDatabaseAsync();

if (args.Contains("--seed"))
{
    await app.SeedDatabaseAsync();
    return;
}

app.Run();

// Pública para que WebApplicationFactory<Program> (tests de integración) la use como entry point.
public partial class Program;
