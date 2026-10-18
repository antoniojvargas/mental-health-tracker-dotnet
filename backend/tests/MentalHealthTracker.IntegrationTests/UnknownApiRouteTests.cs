using System.Net;
using System.Text.Json;

namespace MentalHealthTracker.IntegrationTests;

[Collection(IntegrationTestGroup.Name)]
public sealed class UnknownApiRouteTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory), IClassFixture<CustomWebApplicationFactory>
{
    [Theory]
    [InlineData("GET", "/api/does-not-exist")]
    [InlineData("POST", "/api/logs/does/not/exist")]
    [InlineData("DELETE", "/api/auth/nope")]
    [InlineData("DELETE", "/api/logs")]
    public async Task UnknownApiRoute_Returns404WithNotFoundEnvelope(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        var response = await Factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = body.RootElement.GetProperty("error");
        Assert.Equal("NOT_FOUND", error.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task KnownApiRoute_IsNotShadowedByTheFallback()
    {
        var response = await Factory.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HubRoute_IsNotHandledByTheApiFallback()
    {
        var response = await Factory.CreateClient().PostAsync("/hub/logs/negotiate?negotiateVersion=1", null);

        // Sin cookie, el hub responde su propio 401, no el NOT_FOUND del fallback de /api.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
