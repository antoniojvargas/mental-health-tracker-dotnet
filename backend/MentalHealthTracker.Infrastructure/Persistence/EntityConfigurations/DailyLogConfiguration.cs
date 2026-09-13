using MentalHealthTracker.Domain.Entities;
using MentalHealthTracker.Domain.Enums;
using MentalHealthTracker.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentalHealthTracker.Infrastructure.Persistence.EntityConfigurations;

public sealed class DailyLogConfiguration : IEntityTypeConfiguration<DailyLog>
{
    public void Configure(EntityTypeBuilder<DailyLog> builder)
    {
        builder.ToTable("daily_logs");

        builder.HasKey(log => log.Id);

        builder.Property(log => log.Id)
            .HasColumnName("id")
            .HasColumnType("uuid");
        builder.Property(log => log.UserId).HasColumnName("user_id").HasColumnType("uuid");
        builder.Property(log => log.LogDate).HasColumnName("log_date").HasColumnType("date");
        // Las escalas numéricas (mood_rating, anxiety_level, stress_level, sleep_quality) se
        // modelan como smallint con el rango validado en la capa de aplicación y no como ENUM
        // de Postgres: son valores que se promedian y grafican en tendencias, no categorías
        // cerradas, y evitar el ENUM ahorra una migración de esquema cada vez que se agrega
        // un valor al rango.
        builder.Property(log => log.MoodRating).HasColumnName("mood_rating").HasColumnType("smallint");
        builder.Property(log => log.AnxietyLevel).HasColumnName("anxiety_level").HasColumnType("smallint");
        builder.Property(log => log.StressLevel).HasColumnName("stress_level").HasColumnType("smallint");
        builder.Property(log => log.SleepHours).HasColumnName("sleep_hours").HasPrecision(3, 1);
        builder.Property(log => log.SleepQuality).HasColumnName("sleep_quality").HasColumnType("smallint");
        builder.Property(log => log.SleepDisturbances)
            .HasColumnName("sleep_disturbances")
            .HasColumnType("text[]")
            .HasConversion<Converters.EnumListToStringArrayConverter<SleepDisturbance>>();
        builder.Property(log => log.ActivityType)
            .HasColumnName("activity_type")
            .HasConversion<Converters.EnumSnakeCaseConverter<ActivityType>>();
        builder.Property(log => log.ActivityMinutes).HasColumnName("activity_minutes").HasColumnType("smallint");
        builder.Property(log => log.SocialFrequency)
            .HasColumnName("social_frequency")
            .HasConversion<Converters.EnumSnakeCaseConverter<SocialFrequency>>();
        // Symptoms se guardan como JSON en el propio log y no se normalizan a una tabla
        // hija: siempre se leen y escriben completos junto al log, y nunca se consultan
        // por un síntoma aislado. Una tabla hija añadiría joins y complejidad sin valor real.
        builder.Property(log => log.Symptoms)
            .HasColumnName("symptoms")
            .HasColumnType("jsonb")
            .HasConversion<Converters.JsonListConverter<Symptom>>();
        builder.Property(log => log.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(log => log.CreatedAt).HasColumnName("created_at");
        builder.Property(log => log.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(log => log.UserId);
        builder.HasIndex(log => new { log.UserId, log.LogDate }).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(log => log.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}