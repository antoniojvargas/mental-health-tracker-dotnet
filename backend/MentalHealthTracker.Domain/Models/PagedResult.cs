namespace MentalHealthTracker.Domain.Models;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total);