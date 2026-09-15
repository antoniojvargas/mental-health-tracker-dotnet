namespace MentalHealthTracker.Api.Modules.DailyLog.Dtos;

public sealed record DailyLogListResponse(
    IReadOnlyList<DailyLogResponse> Data,
    DailyLogListMeta Meta);