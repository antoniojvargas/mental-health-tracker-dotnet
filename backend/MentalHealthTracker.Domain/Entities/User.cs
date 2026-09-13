namespace MentalHealthTracker.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }

    public string GoogleId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}