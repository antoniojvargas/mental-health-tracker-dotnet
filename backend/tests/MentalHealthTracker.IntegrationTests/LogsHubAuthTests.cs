using System.Net;
using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Domain.Models;
using MentalHealthTracker.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealthTracker.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class LogsHubAuthTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory), IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Negotiate_WithoutCookie_Returns401()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsync(
            "/hub/logs/negotiate?negotiateVersion=1",
            new StringContent("", System.Text.Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Negotiate_WithTamperedCookie_Returns401()
    {
        var (_, token) = await CreateAuthenticatedSessionAsync();
        var tampered = token.Length > 0
            ? token[..^1] + (token[^1] == 'A' ? 'B' : 'A')
            : token;

        var client = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/hub/logs/negotiate?negotiateVersion=1");
        request.Content = new StringContent("", System.Text.Encoding.UTF8, "text/plain");
        request.Headers.TryAddWithoutValidation("Cookie", $"{JwtService.SessionCookieName}={tampered}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Negotiate_WithValidCookie_Returns200()
    {
        var (_, token) = await CreateAuthenticatedSessionAsync();

        var client = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/hub/logs/negotiate?negotiateVersion=1");
        request.Content = new StringContent("", System.Text.Encoding.UTF8, "text/plain");
        request.Headers.TryAddWithoutValidation("Cookie", $"{JwtService.SessionCookieName}={token}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<(Domain.Entities.User User, string Token)> CreateAuthenticatedSessionAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var jwtService = scope.ServiceProvider.GetRequiredService<JwtService>();

        var email = $"e2e-{Guid.NewGuid():N}@example.com";
        var user = await repository.UpsertByGoogleIdAsync(
            new GoogleProfile($"e2e-{Guid.NewGuid():N}", email, "Integration Test User", null));

        return (user, jwtService.Sign(user.Id, user.Email));
    }
}