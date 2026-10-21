using FluentValidation;
using MentalHealthTracker.Api.Modules.DailyLog;
using MentalHealthTracker.Api.Modules.DailyLog.Dtos;
using MentalHealthTracker.Api.Modules.Realtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MentalHealthTracker.UnitTests;

public sealed class DailyLogAndRealtimeModuleTests
{
    [Fact]
    public void AddDailyLogModule_RegistersTheScopedServiceAndNoValidators()
    {
        var services = new ServiceCollection().AddDailyLogModule();

        AssertRegistration<IDailyLogService, DailyLogService>(services, ServiceLifetime.Scoped);
        Assert.DoesNotContain(services, d => d.ServiceType.IsGenericType
            && d.ServiceType.GetGenericTypeDefinition() == typeof(IValidator<>));
    }

    [Fact]
    public void AddRealtimeModule_RegistersTheTrackerTheEmitterAndTheShutdownService()
    {
        var services = new ServiceCollection().AddRealtimeModule();

        AssertRegistration<SignalRConnectionTracker, SignalRConnectionTracker>(services, ServiceLifetime.Singleton);
        AssertRegistration<ILogEventEmitter, LogEventEmitter>(services, ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IHostedService)
                && d.ImplementationType?.Name == "GracefulShutdownService"
                && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, d => d.ServiceType == typeof(IHubContext<>)); // lo registra AddSignalR
    }

    [Fact]
    public void ModulesReturnTheSameCollectionForChaining()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddDailyLogModule());
        Assert.Same(services, services.AddRealtimeModule());
    }

    [Fact]
    public void TheHubPathIsTheOneTheClientsUse()
    {
        // frontend/src/services/hub-client.ts, nginx.conf y vite.config.ts apuntan a /hub/logs.
        Assert.Equal("/hub/logs", HubRoutes.Logs);
    }

    [Fact]
    public void MapRealtimeHubs_MapsTheLogsHubAtTheConstantPath()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRealtimeModule();
        var app = builder.Build();

        app.MapRealtimeHubs();

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToList();
        Assert.Contains(HubRoutes.Logs, routes);
        Assert.Contains(HubRoutes.Logs + "/negotiate", routes);
    }

    private static void AssertRegistration<TService, TImplementation>(IServiceCollection services, ServiceLifetime lifetime) =>
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(TService)
                && d.ImplementationType == typeof(TImplementation)
                && d.Lifetime == lifetime);
}
