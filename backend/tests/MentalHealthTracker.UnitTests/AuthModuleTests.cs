using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Domain.Entities;
using MentalHealthTracker.Domain.Models;
using MentalHealthTracker.Domain.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MentalHealthTracker.UnitTests;

public sealed class AuthModuleTests
{
    private static List<string> MappedRoutes(string environmentName)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environmentName });

        // Minimal API infiere los parámetros del handler al mapear, y para ello necesita ver
        // los servicios registrados; con el módulo y un repositorio falso basta (no se invoca).
        builder.Services.AddAuthModule();
        builder.Services.AddOptions<MentalHealthTracker.Api.Core.Configuration.JwtOptions>();
        builder.Services.AddSingleton<IUserRepository, UnusedUserRepository>();
        var app = builder.Build();

        app.MapAuthEndpoints(app.Environment);

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToList();
    }

    [Fact]
    public void MapAuthEndpoints_InTesting_MapsTestLogin()
    {
        Assert.Contains("/api/auth/test-login", MappedRoutes("Testing"));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void MapAuthEndpoints_OutsideTesting_MapsNothing(string environmentName)
    {
        Assert.Empty(MappedRoutes(environmentName));
    }

    [Fact]
    public void AddAuthModule_RegistersTheJwtServiceAndCookieOptionsAsSingletonsAndTheTypedGoogleClient()
    {
        var services = new ServiceCollection();
        services.AddAuthModule();

        Assert.Contains(services, d => d.ServiceType == typeof(JwtService) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, d => d.ServiceType == typeof(AuthCookieOptions) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, d => d.ServiceType == typeof(GoogleOAuthClient));
    }

    [Fact]
    public void AddAuthModule_ReturnsTheSameCollectionForChaining()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddAuthModule());
    }

    private sealed class UnusedUserRepository : IUserRepository
    {
        public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<User> UpsertByGoogleIdAsync(GoogleProfile profile, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
