using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Domain.Entities;
using MentalHealthTracker.Domain.Enums;
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

    [Fact]
    public async Task GetLogs_WhenRangeProvided_ReturnsOnlyLogsWithinRange()
    {
        var (user, token) = await CreateAuthenticatedSessionAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedLogAsync(user.Id, today.AddDays(-30), 1);
        await SeedLogAsync(user.Id, today.AddDays(-20), 2);
        await SeedLogAsync(user.Id, today.AddDays(-10), 3);
        await SeedLogAsync(user.Id, today, 4);

        var response = await GetLogsAsync(
            token,
            $"from={today.AddDays(-25):yyyy-MM-dd}&to={today.AddDays(-15):yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("meta").GetProperty("total").GetInt32());
        Assert.Equal(1, root.GetProperty("data").GetArrayLength());
        Assert.Equal(
            today.AddDays(-20).ToString("yyyy-MM-dd"),
            root.GetProperty("data")[0].GetProperty("logDate").GetString());
    }

    [Fact]
    public async Task GetLogs_ReturnsLogsOrderedAscendingByDate()
    {
        var (user, token) = await CreateAuthenticatedSessionAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedLogAsync(user.Id, today, 5);
        await SeedLogAsync(user.Id, today.AddDays(-5), 1);
        await SeedLogAsync(user.Id, today.AddDays(-2), 3);

        var response = await GetLogsAsync(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = document.RootElement.GetProperty("data");
        Assert.Equal(3, data.GetArrayLength());
        Assert.Equal(3, document.RootElement.GetProperty("meta").GetProperty("total").GetInt32());

        var dates = data.EnumerateArray()
            .Select(e => DateOnly.Parse(e.GetProperty("logDate").GetString()!))
            .ToArray();
        for (var i = 1; i < dates.Length; i++)
        {
            Assert.True(dates[i - 1] <= dates[i]);
        }
    }

    [Fact]
    public async Task GetLogs_WhenLimitAndOffsetProvided_PaginatesAndKeepsMetaTotalConsistent()
    {
        var (user, token) = await CreateAuthenticatedSessionAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var i = 0; i < 5; i++)
        {
            await SeedLogAsync(user.Id, today.AddDays(-(2 * i)), i + 1);
        }

        var page1 = await GetLogsAsync(token, "limit=2&offset=0");
        var page2 = await GetLogsAsync(token, "limit=2&offset=2");
        var page3 = await GetLogsAsync(token, "limit=2&offset=4");

        var pageSizes = new[] { page1, page2, page3 }.Select(r => r.StatusCode).ToList();
        Assert.All(pageSizes, c => Assert.Equal(HttpStatusCode.OK, c));

        var total1 = await TotalAsync(page1);
        var total2 = await TotalAsync(page2);
        var total3 = await TotalAsync(page3);

        Assert.Equal(5, total1);
        Assert.Equal(5, total2);
        Assert.Equal(5, total3);

        Assert.Equal(2, await CountAsync(page1));
        Assert.Equal(2, await CountAsync(page2));
        Assert.Equal(1, await CountAsync(page3));
    }

    [Fact]
    public async Task Logs_AreIsolatedPerUser()
    {
        var (userA, tokenA) = await CreateAuthenticatedSessionAsync();
        var (_, tokenB) = await CreateAuthenticatedSessionAsync();

        var threeDaysAgo = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3).ToString("yyyy-MM-dd");
        Assert.NotNull(userA);

        var createdToday = await PostLogAsync(tokenA, Body());
        var createdPast = await PostLogAsync(tokenA, Body(moodRating: 4, logDate: threeDaysAgo));
        Assert.Equal(HttpStatusCode.Created, createdToday.StatusCode);
        Assert.Equal(HttpStatusCode.Created, createdPast.StatusCode);

        var logsA = await GetLogsAsync(tokenA);
        Assert.Equal(HttpStatusCode.OK, logsA.StatusCode);
        Assert.Equal(2, await TotalAsync(logsA));

        var todayA = await GetTodayAsync(tokenA);
        Assert.Equal(HttpStatusCode.OK, todayA.StatusCode);

        var logsB = await GetLogsAsync(tokenB);
        Assert.Equal(HttpStatusCode.OK, logsB.StatusCode);
        Assert.Equal(0, await TotalAsync(logsB));
        Assert.Equal(0, await CountAsync(logsB));

        var todayB = await GetTodayAsync(tokenB);
        Assert.Equal(HttpStatusCode.NotFound, todayB.StatusCode);
    }

    private async Task<HttpResponseMessage> GetTodayAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/logs/today");
        request.Headers.TryAddWithoutValidation("Cookie", $"{JwtService.SessionCookieName}={token}");

        return await _factory.CreateClient().SendAsync(request);
    }

    private async Task<int> TotalAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("meta").GetProperty("total").GetInt32();
    }

    private async Task<int> CountAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").GetArrayLength();
    }

    private async Task SeedLogAsync(Guid userId, DateOnly date, int mood)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDailyLogRepository>();
        await repository.UpsertAsync(new DailyLog
        {
            UserId = userId,
            LogDate = date,
            MoodRating = (short)mood,
            AnxietyLevel = 3,
            StressLevel = 3,
            SleepHours = 7m,
            SleepQuality = 3,
            SleepDisturbances = [],
            ActivityType = null,
            ActivityMinutes = null,
            SocialFrequency = SocialFrequency.Occasional,
            Symptoms = [],
            Notes = null,
        });
    }

    private async Task<HttpResponseMessage> PostLogAsync(string token, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/logs");
        request.Headers.TryAddWithoutValidation("Cookie", $"{JwtService.SessionCookieName}={token}");
        request.Content = JsonContent.Create(body);

        return await _factory.CreateClient().SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetLogsAsync(string token, string? query = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/logs{(query is null ? "" : $"?{query}")}");
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

    private static object Body(int moodRating = 3, string? notes = null, string? logDate = null) => new
    {
        logDate = logDate ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
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