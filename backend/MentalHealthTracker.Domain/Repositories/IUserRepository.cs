using MentalHealthTracker.Domain.Entities;
using MentalHealthTracker.Domain.Models;

namespace MentalHealthTracker.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User> UpsertByGoogleIdAsync(GoogleProfile profile, CancellationToken cancellationToken = default);
}