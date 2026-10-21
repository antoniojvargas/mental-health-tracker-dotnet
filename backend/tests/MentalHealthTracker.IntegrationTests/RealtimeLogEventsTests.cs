using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using MentalHealthTracker.Api.Core.Serialization;
using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Api.Modules.DailyLog.Dtos;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealthTracker.IntegrationTests;

[Collection(IntegrationTestGroup.Name)]
public sealed class RealtimeLogEventsTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly string[] NoDisturbances = ["none"];

    [Fact]
    public async Task PostOwnLog_OwnConnectedClient_ReceivesLogCreated()
    {
        var (_, token) = await CreateSessionTokenAsync();
        var received = new TaskCompletionSource<DailyLogResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using var connection = CreateHubConnection(token);
        connection.On<DailyLogResponse>("log:created", received.SetResult);
        await connection.StartAsync();

        await PostLogAsync(token);

        var log = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), log.LogDate);
        Assert.Equal(3, log.MoodRating);
        Assert.Equal("mejor que ayer", log.Notes);
    }

    [Fact]
    public async Task PostOwnLog_OtherUsersClient_ReceivesNothing()
    {
        var (_, ownerToken) = await CreateSessionTokenAsync();
        var (_, otherToken) = await CreateSessionTokenAsync();

        var ownerReceived = new TaskCompletionSource<DailyLogResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var otherReceived = new TaskCompletionSource<DailyLogResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using var ownerConnection = CreateHubConnection(ownerToken);
        await using var otherConnection = CreateHubConnection(otherToken);
        ownerConnection.On<DailyLogResponse>("log:created", ownerReceived.SetResult);
        otherConnection.On<DailyLogResponse>("log:created", otherReceived.SetResult);
        await ownerConnection.StartAsync();
        await otherConnection.StartAsync();

        await PostLogAsync(ownerToken);

        await ownerReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var otherGotEvent = await Task.WhenAny(
            otherReceived.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.NotSame(otherReceived.Task, otherGotEvent);
    }

    private HubConnection CreateHubConnection(string token)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(Factory.Server.BaseAddress, "hub/logs"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                options.Headers["Cookie"] = $"{SessionCookie.AccessTokenName}={token}";
            })
            .AddJsonProtocol(options =>
                options.PayloadSerializerOptions.Converters.Add(new SnakeCaseEnumJsonConverter()))
            .Build();
    }

    private async Task<HttpResponseMessage> PostLogAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/logs");
        request.Headers.TryAddWithoutValidation("Cookie", $"{SessionCookie.AccessTokenName}={token}");
        request.Content = JsonBody(new CreateDailyLogRequestBuilder().WithNotes("mejor que ayer").Build());

        return await Factory.CreateClient().SendAsync(request);
    }
}
