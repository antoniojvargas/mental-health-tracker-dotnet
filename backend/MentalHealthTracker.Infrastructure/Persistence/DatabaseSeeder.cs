using MentalHealthTracker.Domain.Entities;
using MentalHealthTracker.Domain.Enums;
using MentalHealthTracker.Domain.Models;
using MentalHealthTracker.Domain.ValueObjects;
using MentalHealthTracker.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace MentalHealthTracker.Infrastructure.Persistence;

// Genera un usuario demo con 60 días de registros pseudoaleatorios pero coherentes:
// un mismo Random(42) hace el dataset determinista (misma salida en cada ejecución) y
// los upserts por clave natural lo hacen idempotente (no duplica datos al repetirlo).
// La coherencia se logra derivando las métricas de valores semilla: el ánimo y la
// calidad de sueño se correlacionan con las horas dormidas (días de mal sueño = peor
// ánimo), y la ansiedad/estrés son inversos al ánimo. Algunos días no hay actividad
// registrada, como en los datos reales.
public sealed class DatabaseSeeder(AppDbContext dbContext, ILogger<DatabaseSeeder> logger)
{
    private const string DemoGoogleId = "demo-user";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var userRepository = new UserRepository(dbContext);
        var logRepository = new DailyLogRepository(dbContext);

        var user = await userRepository.UpsertByGoogleIdAsync(
            new GoogleProfile(DemoGoogleId, "demo@mentalhealthtracker.dev", "Demo User", null),
            cancellationToken);

        logger.LogInformation("Seeding 60 daily logs for demo user {UserId}", user.Id);

        var random = new Random(42);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = today.AddDays(-59);

        for (var i = 0; i < 60; i++)
        {
            var date = start.AddDays(i);
            var log = BuildDailyLog(user.Id, date, i, random);
            await logRepository.UpsertAsync(log, cancellationToken);
        }

        logger.LogInformation("Demo data seeding completed");
    }

    private static DailyLog BuildDailyLog(Guid userId, DateOnly date, int dayIndex, Random random)
    {
        var weekly = Math.Sin(dayIndex * 2 * Math.PI / 7);

        var sleepHours = Math.Clamp(6.6 + weekly * 0.8 + (random.NextDouble() - 0.5) * 1.2, 4.0, 9.0);
        var mood = Math.Clamp((int)Math.Round(5 + (sleepHours - 6.0) * 1.5 + (random.NextDouble() - 0.5) * 4), 1, 10);
        var sleepQuality = Math.Clamp(
            (int)Math.Round(5.5 + (sleepHours - 6.0) * 2 + (random.NextDouble() - 0.5) * 3), 1, 10);
        var anxiety = Math.Clamp((int)Math.Round(11 - mood + (random.NextDouble() - 0.5) * 3), 1, 10);
        var stress = Math.Clamp((int)Math.Round(11 - mood + (random.NextDouble() - 0.5) * 2), 1, 10);

        var discomfort = new List<SleepDisturbance>();
        if (sleepHours < 5.5 || mood < 4)
        {
            discomfort.Add(random.Next(2) == 0 ? SleepDisturbance.Insomnia : SleepDisturbance.FrequentWaking);
        }

        if (random.NextDouble() < 0.15 && sleepHours < 6.5)
        {
            discomfort.Add(SleepDisturbance.EarlyWaking);
        }

        if (discomfort.Count == 0)
        {
            discomfort.Add(SleepDisturbance.None);
        }

        ActivityType? activityType = null;
        short? activityMinutes = null;
        if (random.NextDouble() >= 0.25)
        {
            activityType = random.NextDouble() switch
            {
                < 0.30 => ActivityType.Walking,
                < 0.50 => ActivityType.Running,
                < 0.70 => ActivityType.Gym,
                < 0.85 => ActivityType.Yoga,
                _ => ActivityType.Cycling,
            };
            activityMinutes = (short)random.Next(25, 91);
        }

        var social = mood switch
        {
            >= 8 => SocialFrequency.Daily,
            >= 6 => SocialFrequency.Frequent,
            >= 4 => SocialFrequency.Occasional,
            _ => SocialFrequency.Rare,
        };

        var symptoms = new List<Symptom>();
        if (mood <= 3)
        {
            symptoms.Add(new Symptom(SymptomType.LowMood, random.Next(4, 6)));
            symptoms.Add(new Symptom(SymptomType.Fatigue, random.Next(3, 6)));
            if (random.NextDouble() < 0.7)
            {
                symptoms.Add(new Symptom(SymptomType.Irritability, random.Next(2, 5)));
            }

            symptoms.Add(new Symptom(SymptomType.Restlessness, random.Next(2, 4)));
        }
        else if (mood <= 5)
        {
            symptoms.Add(new Symptom(SymptomType.Fatigue, random.Next(2, 4)));
            if (random.NextDouble() < 0.5)
            {
                symptoms.Add(new Symptom(SymptomType.LowMood, random.Next(1, 3)));
            }
        }

        string? notes = null;
        if (random.NextDouble() < 0.18)
        {
            notes = DailyNotes[random.Next(DailyNotes.Length)];
        }

        return new DailyLog
        {
            UserId = userId,
            LogDate = date,
            MoodRating = (short)mood,
            AnxietyLevel = (short)anxiety,
            StressLevel = (short)stress,
            SleepHours = (decimal)Math.Round(sleepHours, 1),
            SleepQuality = (short)sleepQuality,
            SleepDisturbances = discomfort,
            ActivityType = activityType,
            ActivityMinutes = activityMinutes,
            SocialFrequency = social,
            Symptoms = symptoms,
            Notes = notes,
        };
    }

    private static readonly string[] DailyNotes =
    [
        "Día tranquilo en el trabajo.",
        "Salí a caminar después de comer.",
        "Me costó dormir, demasiado café.",
        "Llamada larga con la familia.",
        "Poca concentración hoy.",
        "Buen día, me sentí con energía.",
        "Un poco de ansiedad por la mañana.",
        "Día de teletrabajo en casa.",
    ];
}