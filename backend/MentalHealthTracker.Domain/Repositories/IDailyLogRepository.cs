using MentalHealthTracker.Domain.Entities;
using MentalHealthTracker.Domain.Models;

namespace MentalHealthTracker.Domain.Repositories;

public interface IDailyLogRepository
{
    Task<DailyLog?> FindByUserAndDateAsync(Guid userId, DateOnly date, CancellationToken cancellationToken = default);

    Task<PagedResult<DailyLog>> FindByUserAndRangeAsync(
        Guid userId,
        DateOnly from,
        DateOnly to,
        int limit,
        int offset,
        CancellationToken cancellationToken = default);

    Task<DailyLog> UpsertAsync(DailyLog log, CancellationToken cancellationToken = default);
}