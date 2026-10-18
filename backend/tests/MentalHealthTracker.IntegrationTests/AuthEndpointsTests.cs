using System.Net;
using System.Text.Json;
using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Domain.Models;
using MentalHealthTracker.Domain.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using MentalHealthTracker.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealthTracker.IntegrationTests;

[Collection(IntegrationTestGroup.Name)]
public sealed class AuthEndpointsTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory), IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Me_WithoutCookie_Returns401()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidCookieForDeletedUser_Returns401WithErrorEnvelope()
    {
        var (client, _, _) = await CreateAuthenticatedSessionAsync("deleted-user@example.com");

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE users, daily_logs CASCADE");
        }

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = body.RootElement.GetProperty("error");
        Assert.Equal("UNAUTHORIZED", error.GetProperty("code").GetString());
        Assert.Equal("Authentication required", error.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Me_WithValidCookie_ReturnsUser()
    {
        var (user, token) = await CreateAuthenticatedSessionAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.TryAddWithoutValidation("Cookie", $"{JwtService.SessionCookieName}={token}");

        var response = await Factory.CreateClient().SendAsync(request);

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
        var tampered = TamperSignature(token);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.TryAddWithoutValidation("Cookie", $"{JwtService.SessionCookieName}={tampered}");

        var response = await Factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_ClearsSessionCookie()
    {
        var (_, token) = await CreateAuthenticatedSessionAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.TryAddWithoutValidation("Cookie", $"{JwtService.SessionCookieName}={token}");

        var response = await Factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var setCookie = string.Join("; ", response.Headers.TryGetValues("Set-Cookie", out var values) ? values : []);
        Assert.Contains("access_token=", setCookie);
        Assert.True(
            setCookie.Contains("Max-Age=0", StringComparison.OrdinalIgnoreCase) ||
            setCookie.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase),
            $"Set-Cookie debe expirar la cookie de sesión, pero fue: {setCookie}");
    }

    [Fact]
    public async Task GoogleLogin_SetsStateCookieAndRedirectsToGoogle()
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/auth/google");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);

        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("oauth_state=", setCookie, StringComparison.Ordinal);
        Assert.Contains("HttpOnly", setCookie, StringComparison.OrdinalIgnoreCase);

        var stateFromCookie = setCookie.Split(';')[0].Split('=', 2)[1];
        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);

        Assert.Equal(stateFromCookie, query["state"].ToString());
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("openid email profile", query["scope"]);
        Assert.Equal("online", query["access_type"]);
        Assert.Equal("select_account", query["prompt"]);
        Assert.Equal("integration-test-client.apps.googleusercontent.com", query["client_id"]);
        Assert.Equal("http://localhost:5173/api/auth/google/callback", query["redirect_uri"]);
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