namespace MentalHealthTracker.Api.Modules.DailyLog.Dtos;

public sealed record DailyLogListMeta(
    DateOnly From,
    DateOnly To,
    int Limit,
    int Offset,
    int Total);