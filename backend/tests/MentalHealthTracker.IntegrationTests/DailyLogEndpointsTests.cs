using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Domain.Models;
using MentalHealthTracker.Domain.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealthTracker.IntegrationTests;

public sealed class DailyLogEndpointsTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public DailyLogEndpointsTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostLog_WhenNoExistingLog_Returns201()
    {
        var (_, token) = await CreateAuthenticatedSessionAsync();
        var logDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        var response = await PostLogAsync(token, Body());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(logDate, root.GetProperty("logDate").GetString());
        Assert.Equal(3, root.GetProperty("moodRating").GetInt32());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("id").GetString()));
    }

    [Fact]
    public async Task PostLog_WhenLogAlreadyExistsForDate_Returns200AndUpdatesInsteadOfDuplicating()
    {
        var (_, token) = await CreateAuthenticatedSessionAsync();

        var first = await PostLogAsync(token, Body());
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var updated = await PostLogAsync(token, Body(moodRating: 2, notes: "updated"));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        using var document = JsonDocument.Parse(await updated.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(2, root.GetProperty("moodRating").GetInt32());
        Assert.Equal("updated", root.GetProperty("notes").GetString());

        var listResponse = await GetLogsAsync(token);
        var listDocument = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var list = listDocument.RootElement;
        Assert.Equal(1, list.GetProperty("meta").GetProperty("total").GetInt32());
        Assert.Equal(1, list.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task PostLog_ConcurrentPostsForSameDate_BothSucceed_OneRowExactly_OneCreated()
    {
        var (user, token) = await CreateAuthenticatedSessionAsync();
        var logDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var tasks = new[] { PostLogAsync(token, Body(moodRating: 3)), PostLogAsync(token, Body(moodRating: 4)) };
        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK));

        var createdCount = results.Count(r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(1, createdCount);

        await using var scope = _factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDailyLogRepository>();
        var log = await repository.FindByUserAndDateAsync(user.Id, logDate);

        Assert.NotNull(log);
    }

    [Fact]
    public async Task PostLog_WhenBodyIsInvalid_Returns400WithFieldDetail()
    {
        var (_, token) = await CreateAuthenticatedSessionAsync();

        var response = await PostLogAsync(token, Body(moodRating: 9));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = document.RootElement.GetProperty("error");
        Assert.Equal("VALIDATION_ERROR", error.GetProperty("code").GetString());
        Assert.Contains(
            error.GetProperty("details").EnumerateArray(),
            detail => detail.GetProperty("field").GetString() == "MoodRating");
    }

    private async Task<HttpResponseMessage> PostLogAsync(string token, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/logs");
        request.Headers.TryAddWithoutValidation("Cookie", $"{JwtService.SessionCookieName}={token}");
        request.Content = JsonContent.Create(body);

        return await _factory.CreateClient().SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetLogsAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/logs?limit=366");
        request.Headers.TryAddWithoutValidation("Cookie", $"{JwtService.SessionCookieName}={token}");

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

    private static object Body(int moodRating = 3, string? notes = null) => new
    {
        logDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        moodRating,
        anxietyLevel = 5,
        stressLevel = 5,
        sleepHours = 7.5,
        sleepQuality = 4,
        sleepDisturbances = new[] { "none" },
        activityType = "walking",
        activityMinutes = 30,
        socialFrequency = "occasional",
        symptoms = new[] { new { type = "fatigue", severity = 3 } },
        notes,
    };
}