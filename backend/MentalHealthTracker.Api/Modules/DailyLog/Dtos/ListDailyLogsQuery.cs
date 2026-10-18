namespace MentalHealthTracker.Api.Modules.DailyLog.Dtos;

public sealed record ListDailyLogsQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    int Limit = DailyLogDefaults.DefaultLimit,
    int Offset = 0);