namespace MentalHealthTracker.Api.Modules.DailyLog.Dtos;

public sealed record ListDailyLogsQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    int Limit = 100,
    int Offset = 0);