using System.Net;

namespace MentalHealthTracker.IntegrationTests;

[Collection(IntegrationTestGroup.Name)]
public sealed class LogsHubAuthTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
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
        var (_, token) = await CreateSessionTokenAsync();
        var tampered = TamperSignature(token);

        var response = await Factory.CreateClient().SendAuthenticatedAsync(
            HttpMethod.Post,
            "/hub/logs/negotiate?negotiateVersion=1",
            tampered,
            new StringContent("", System.Text.Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Negotiate_WithValidCookie_Returns200()
    {
        var (_, token) = await CreateSessionTokenAsync();

        var response = await Factory.CreateClient().SendAuthenticatedAsync(
            HttpMethod.Post,
            "/hub/logs/negotiate?negotiateVersion=1",
            token,
            new StringContent("", System.Text.Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
