namespace MentalHealthTracker.Api.Modules.DailyLog.Dtos;

public sealed record ListDailyLogsQuery(
    DateOnly From,
    DateOnly To,
    int Limit = 100,
    int Offset = 0);