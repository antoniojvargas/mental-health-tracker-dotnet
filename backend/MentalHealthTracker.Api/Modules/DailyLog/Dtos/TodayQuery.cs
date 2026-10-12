namespace MentalHealthTracker.Api.Modules.DailyLog.Dtos;

// `Date` es el día del cliente en su zona horaria. Sin él, el servidor usa el día UTC.
public sealed record TodayQuery(DateOnly? Date = null);
