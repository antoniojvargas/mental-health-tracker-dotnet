using System.Globalization;
using System.Net;
using System.Text.Json;
using MentalHealthTracker.Domain.Entities;
using MentalHealthTracker.Domain.Enums;
using MentalHealthTracker.Domain.Models;
using MentalHealthTracker.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealthTracker.IntegrationTests;

// GET /api/logs/today?date=yyyy-MM-dd: el día lo decide el cliente en su zona horaria.
// Sin `date` se conserva el comportamiento anterior (día UTC del servidor). El servidor solo
// acota la fecha a "UTC + 1 día", porque UTC+14 es la zona más adelantada.
[Collection(IntegrationTestGroup.Name)]
public sealed class TodayEndpointTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private static DateOnly UtcToday => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Today_WithDate_ReturnsTheLogOfThatDay()
    {
        var (_, user, token) = await NewSessionAsync();
        await SeedLogAsync(user.Id, UtcToday, mood: 5);
        await SeedLogAsync(user.Id, UtcToday.AddDays(-1), mood: 2);

        var response = await GetTodayAsync(token, $"date={UtcToday.AddDays(-1):yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(UtcToday.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), body.GetProperty("logDate").GetString());
        Assert.Equal(2, body.GetProperty("moodRating").GetInt32());
    }

    [Fact]
    public async Task Today_WithDateOneDayAheadOfUtc_IsAcceptedForClientsEastOfUtc()
    {
        var (_, user, token) = await NewSessionAsync();
        var tomorrow = UtcToday.AddDays(1);
        await SeedLogAsync(user.Id, tomorrow, mood: 4);

        var response = await GetTodayAsync(token, $"date={tomorrow:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(4, body.GetProperty("moodRating").GetInt32());
    }

    [Fact]
    public async Task Today_WithDateWithoutLog_ReturnsNotFound()
    {
        var (_, _, token) = await NewSessionAsync();

        var response = await GetTodayAsync(token, $"date={UtcToday.AddDays(-3):yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Today_WithDateTwoDaysAheadOfUtc_ReturnsValidationError()
    {
        var (_, _, token) = await NewSessionAsync();

        var response = await GetTodayAsync(token, $"date={UtcToday.AddDays(2):yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadJsonAsync(response);
        var error = body.GetProperty("error");
        Assert.Equal("VALIDATION_ERROR", error.GetProperty("code").GetString());
        Assert.True(error.GetProperty("details").GetRawText().Contains("date", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("date=abc")]
    [InlineData("date=2026-13-45")]
    public async Task Today_WithMalformedDate_ReturnsBadRequest(string query)
    {
        var (_, _, token) = await NewSessionAsync();

        var response = await GetTodayAsync(token, query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Today_WithoutDate_KeepsUsingTheUtcDay()
    {
        var (_, user, token) = await NewSessionAsync();
        await SeedLogAsync(user.Id, UtcToday, mood: 5);
        await SeedLogAsync(user.Id, UtcToday.AddDays(-1), mood: 2);

        var response = await GetTodayAsync(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(UtcToday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), body.GetProperty("logDate").GetString());
        Assert.Equal(5, body.GetProperty("moodRating").GetInt32());
    }

    [Fact]
    public async Task Today_WithoutDateAndWithoutLog_ReturnsNotFound()
    {
        var (_, _, token) = await NewSessionAsync();

        var response = await GetTodayAsync(token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Today_WithoutLog_ReturnsNotFoundErrorEnvelope()
    {
        var (_, _, token) = await NewSessionAsync();

        var response = await GetTodayAsync(token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("NOT_FOUND", body.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    private async Task<(HttpClient Client, User User, string Token)> NewSessionAsync() =>
        await CreateAuthenticatedSessionAsync($"today-{Guid.NewGuid():N}@example.com");

    private async Task<HttpResponseMessage> GetTodayAsync(string token, string? query = null)
    {
        return await Factory.CreateClient().SendAuthenticatedAsync(
            HttpMethod.Get,
            $"/api/logs/today{(query is null ? "" : $"?{query}")}",
            token);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private async Task SeedLogAsync(Guid userId, DateOnly date, int mood)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
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
}
