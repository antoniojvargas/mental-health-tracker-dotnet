using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Domain.Models;
using MentalHealthTracker.Domain.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealthTracker.IntegrationTests;

public sealed class AuthEndpointsTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public AuthEndpointsTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Me_WithoutCookie_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidCookie_ReturnsUser()
    {
        var (user, token) = await CreateAuthenticatedSessionAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.TryAddWithoutValidation("Cookie", $"{JwtService.SessionCookieName}={token}");

        var response = await _factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        Assert.Equal(user.Id.ToString(), root.GetProperty("id").GetString());
        Assert.Equal(user.Email, root.GetProperty("email").GetString());
        Assert.Equal(user.Name, root.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("avatarUrl").ValueKind);
    }

    [Fact]
    public async Task Me_WithTamperedCookie_Returns401()
    {
        var (_, token) = await CreateAuthenticatedSessionAsync();
        var tampered = token.Length > 0
            ? token[..^1] + (token[^1] == 'A' ? 'B' : 'A')
            : token;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.TryAddWithoutValidation("Cookie", $"{JwtService.SessionCookieName}={tampered}");

        var response = await _factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_ClearsSessionCookie()
    {
        var (_, token) = await CreateAuthenticatedSessionAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.TryAddWithoutValidation("Cookie", $"{JwtService.SessionCookieName}={token}");

        var response = await _factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var setCookie = string.Join("; ", response.Headers.TryGetValues("Set-Cookie", out var values) ? values : []);
        Assert.Contains("access_token=", setCookie);
        Assert.True(
            setCookie.Contains("Max-Age=0", StringComparison.OrdinalIgnoreCase) ||
            setCookie.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase),
            $"Set-Cookie debe expirar la cookie de sesión, pero fue: {setCookie}");
    }

    [Fact]
    public async Task TestLogin_NotRegistered_WhenEnvironmentIsNotTesting()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/test-login",
            new { email = "e2e@example.com", name = "E2E" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<(Domain.Entities.User User, string Token)> CreateAuthenticatedSessionAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var jwtService = scope.ServiceProvider.GetRequiredService<JwtService>();

        var email = $"e2e-{Guid.NewGuid():N}@example.com";
        var user = await repository.UpsertByGoogleIdAsync(
            new GoogleProfile($"e2e-{Guid.NewGuid():N}", email, "Integration Test User", null));

        return (user, jwtService.Sign(user.Id, user.Email));
    }
}