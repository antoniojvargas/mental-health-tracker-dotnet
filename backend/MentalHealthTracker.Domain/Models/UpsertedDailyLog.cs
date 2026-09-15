using MentalHealthTracker.Domain.Entities;

namespace MentalHealthTracker.Domain.Models;

public sealed record UpsertedDailyLog(DailyLog Log, bool Created);