using MentalHealthTracker.Domain.Entities;
using MentalHealthTracker.Domain.Models;
using MentalHealthTracker.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace MentalHealthTracker.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(AppDbContext dbContext) : IUserRepository
{
    // El upsert se hace por GoogleId, no por email: el email de una cuenta puede
    // cambiar (el usuario lo modifica en su cuenta de Google) y el sub no.
    public async Task<User> UpsertByGoogleIdAsync(GoogleProfile profile, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .SingleOrDefaultAsync(u => u.GoogleId == profile.GoogleId, cancellationToken);

        var now = DateTimeOffset.UtcNow;

        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                GoogleId = profile.GoogleId,
                CreatedAt = now,
            };
            await dbContext.Users.AddAsync(user, cancellationToken);
        }

        user.Email = profile.Email;
        user.Name = profile.Name;
        user.AvatarUrl = profile.AvatarUrl;
        user.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await dbContext.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
}