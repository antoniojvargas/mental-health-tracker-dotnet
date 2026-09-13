using MentalHealthTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentalHealthTracker.Infrastructure.Persistence.EntityConfigurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id)
            .HasColumnName("id")
            .HasColumnType("uuid");
        builder.Property(user => user.GoogleId)
            .HasColumnName("google_id")
            .HasMaxLength(64);
        builder.Property(user => user.Email)
            .HasColumnName("email")
            .HasMaxLength(255);
        builder.Property(user => user.Name)
            .HasColumnName("name")
            .HasMaxLength(255);
        builder.Property(user => user.AvatarUrl).HasColumnName("avatar_url");
        builder.Property(user => user.CreatedAt).HasColumnName("created_at");
        builder.Property(user => user.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(user => user.GoogleId).IsUnique();
        builder.HasIndex(user => user.Email).IsUnique();
    }
}