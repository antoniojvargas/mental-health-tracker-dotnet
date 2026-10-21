using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MentalHealthTracker.Api.Modules.Realtime;

namespace MentalHealthTracker.IntegrationTests;

// A7: el aviso en tiempo real es de mejor esfuerzo. Si el hub falla al enviar, el
// registro ya está confirmado en la base de datos, así que POST /api/logs debe seguir
// respondiendo 201 y el dato debe poder leerse después.
[Collection(IntegrationTestGroup.Name)]
public sealed class RealtimeFailureTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task PostLog_WhenHubSendThrows_StillReturns201AndTheLogIsReadable()
    {
        await using var failing = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IHubContext<LogsHub>, ThrowingHubContext>()));

        var (_, _, token) = await CreateAuthenticatedSessionAsync($"rt-{Guid.NewGuid():N}@example.com");

        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var created = await failing.CreateClient().SendAuthenticatedAsync(
            HttpMethod.Post,
            "/api/logs",
            token,
            JsonBody(new CreateDailyLogRequestBuilder().Build()));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var listed = await failing.CreateClient().SendAuthenticatedAsync(
            HttpMethod.Get,
            $"/api/logs?from={today}&to={today}",
            token);

        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        using var body = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
        var logs = body.RootElement.GetProperty("data").EnumerateArray().ToArray();
        Assert.Single(logs);
        Assert.Equal(today, logs[0].GetProperty("logDate").GetString());
    }

    private sealed class ThrowingHubContext : IHubContext<LogsHub>
    {
        public IHubClients Clients { get; } = new ThrowingHubClients();

        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class ThrowingHubClients : IHubClients
    {
        private static readonly IClientProxy Proxy = new ThrowingClientProxy();

        public IClientProxy All => Proxy;

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy;

        public IClientProxy Client(string connectionId) => Proxy;

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy;

        public IClientProxy Group(string groupName) => Proxy;

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy;

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy;

        public IClientProxy User(string userId) => Proxy;

        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy;
    }

    private sealed class ThrowingClientProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("hub is down");
    }
}
