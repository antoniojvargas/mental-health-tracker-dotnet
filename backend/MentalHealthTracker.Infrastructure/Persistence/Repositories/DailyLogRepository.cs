using MentalHealthTracker.Domain.Entities;
using MentalHealthTracker.Domain.Models;
using MentalHealthTracker.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace MentalHealthTracker.Infrastructure.Persistence.Repositories;

public sealed class DailyLogRepository(AppDbContext dbContext) : IDailyLogRepository
{
    public async Task<DailyLog?> FindByUserAndDateAsync(Guid userId, DateOnly date, CancellationToken cancellationToken = default)
        => await dbContext.DailyLogs
            .SingleOrDefaultAsync(log => log.UserId == userId && log.LogDate == date, cancellationToken);

    public async Task<PagedResult<DailyLog>> FindByUserAndRangeAsync(
        Guid userId,
        DateOnly from,
        DateOnly to,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.DailyLogs
            .Where(log => log.UserId == userId && log.LogDate >= from && log.LogDate <= to);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(log => log.LogDate)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new PagedResult<DailyLog>(items, total);
    }

    public async Task<DailyLog> UpsertAsync(DailyLog log, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.DailyLogs
            .SingleOrDefaultAsync(d => d.UserId == log.UserId && d.LogDate == log.LogDate, cancellationToken);

        var now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            log.Id = Guid.NewGuid();
            log.CreatedAt = now;
            log.UpdatedAt = now;
            await dbContext.DailyLogs.AddAsync(log, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return log;
        }

        existing.MoodRating = log.MoodRating;
        existing.AnxietyLevel = log.AnxietyLevel;
        existing.StressLevel = log.StressLevel;
        existing.SleepHours = log.SleepHours;
        existing.SleepQuality = log.SleepQuality;
        existing.SleepDisturbances = log.SleepDisturbances;
        existing.ActivityType = log.ActivityType;
        existing.ActivityMinutes = log.ActivityMinutes;
        existing.SocialFrequency = log.SocialFrequency;
        existing.Symptoms = log.Symptoms;
        existing.Notes = log.Notes;
        existing.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return existing;
    }
}