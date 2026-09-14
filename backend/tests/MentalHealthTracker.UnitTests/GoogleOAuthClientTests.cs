using System.Net;
using System.Text;
using Google.Apis.Auth;
using MentalHealthTracker.Api.Core.Configuration;
using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Domain.Errors;
using MentalHealthTracker.Domain.Models;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.UnitTests;

public class GoogleOAuthClientTests
{
    private static readonly GoogleOAuthOptions TestGoogleOptions = new()
    {
        ClientId = "test-client.apps.googleusercontent.com",
        ClientSecret = "test-client-secret",
        RedirectUri = "http://localhost:5173/auth/callback",
    };

    [Fact]
    public async Task ExchangeCodeForProfileAsync_WhenTokenResponseHasNoIdToken_Throws()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse("""{ "access_token": "abc123" }"""));
        using var httpClient = new HttpClient(handler);
        var client = new GoogleOAuthClient(httpClient, Options.Create(TestGoogleOptions));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ExchangeCodeForProfileAsync("auth-code"));

        Assert.Contains("id_token", exception.Message);
    }

    [Fact]
    public async Task ExchangeCodeForProfileAsync_WhenValidatedPayloadHasNoSub_ThrowsUnauthorized()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse("""{ "id_token": "signed-token" }"""));
        using var httpClient = new HttpClient(handler);
        var client = new GoogleOAuthClient(httpClient, Options.Create(TestGoogleOptions), PayloadWithoutSub);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => client.ExchangeCodeForProfileAsync("auth-code"));
    }

    [Fact]
    public async Task ExchangeCodeForProfileAsync_WithValidProfile_ReturnsMappedProfile()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return JsonResponse("""{ "id_token": "signed-token" }""");
        });
        using var httpClient = new HttpClient(handler);
        var client = new GoogleOAuthClient(httpClient, Options.Create(TestGoogleOptions), ValidPayload("123456789"));

        var profile = await client.ExchangeCodeForProfileAsync("auth-code");

        var expected = new GoogleProfile(
            GoogleId: "123456789",
            Email: "patient@example.com",
            Name: "Jane Doe",
            AvatarUrl: "http://example.com/avatar.png");
        Assert.Equal(expected, profile);

        var body = await capturedRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("code=auth-code", body);
        Assert.Contains("client_id=test-client.apps.googleusercontent.com", body);
        Assert.Contains("client_secret=test-client-secret", body);
        Assert.Contains("grant_type=authorization_code", body);
    }

    private static Func<string, Task<GoogleJsonWebSignature.Payload>> ValidPayload(string sub) =>
        _ => Task.FromResult(new GoogleJsonWebSignature.Payload
        {
            Subject = sub,
            Email = "patient@example.com",
            EmailVerified = true,
            Name = "Jane Doe",
            Picture = "http://example.com/avatar.png",
        });

    private static Func<string, Task<GoogleJsonWebSignature.Payload>> PayloadWithoutSub =>
        _ => Task.FromResult(new GoogleJsonWebSignature.Payload
        {
            Email = "patient@example.com",
            Name = "Jane Doe",
        });

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }
}