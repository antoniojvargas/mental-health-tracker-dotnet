using MentalHealthTracker.Domain.Entities;
using MentalHealthTracker.Domain.Models;

namespace MentalHealthTracker.Domain.Repositories;

public interface IDailyLogRepository
{
    Task<DailyLog?> FindByUserAndDateAsync(Guid userId, DateOnly logDate, CancellationToken cancellationToken = default);

    Task<PagedResult<DailyLog>> FindByUserAndRangeAsync(
        Guid userId,
        DateOnly fromDate,
        DateOnly toDate,
        int limit,
        int offset,
        CancellationToken cancellationToken = default);

    Task<UpsertedDailyLog> UpsertAsync(DailyLog log, CancellationToken cancellationToken = default);
}