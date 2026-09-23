using System.Net;
using System.Net.Http.Json;
using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Api.Modules.DailyLog.Dtos;
using MentalHealthTracker.Domain.Models;
using MentalHealthTracker.Domain.Repositories;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealthTracker.IntegrationTests;

public sealed class RealtimeLogEventsTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public RealtimeLogEventsTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostOwnLog_OwnConnectedClient_ReceivesLogCreated()
    {
        var (_, token) = await CreateAuthenticatedSessionAsync();
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
        var (_, ownerToken) = await CreateAuthenticatedSessionAsync();
        var (_, otherToken) = await CreateAuthenticatedSessionAsync();

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
            .WithUrl(new Uri(_factory.Server.BaseAddress, "hub/logs"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Headers["Cookie"] = $"{JwtService.SessionCookieName}={token}";
            })
            .Build();
    }

    private async Task<HttpResponseMessage> PostLogAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/logs");
        request.Headers.TryAddWithoutValidation("Cookie", $"{JwtService.SessionCookieName}={token}");
        request.Content = JsonContent.Create(new
        {
            logDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            moodRating = 3,
            anxietyLevel = 5,
            stressLevel = 5,
            sleepHours = 7.5,
            sleepQuality = 4,
            sleepDisturbances = new[] { "none" },
            activityType = "walking",
            activityMinutes = 30,
            socialFrequency = "occasional",
            symptoms = new[] { new { type = "fatigue", severity = 3 } },
            notes = "mejor que ayer",
        });

        return await _factory.CreateClient().SendAsync(request);
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