using MentalHealthTracker.Domain.Entities;
using MentalHealthTracker.Domain.Enums;
using MentalHealthTracker.Domain.Models;
using MentalHealthTracker.Domain.Repositories;
using MentalHealthTracker.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace MentalHealthTracker.Infrastructure.Persistence.Repositories;

public sealed class DailyLogRepository(AppDbContext dbContext) : IDailyLogRepository
{
    // Un select-then-insert del estilo "buscar por (user_id, log_date); si no existe,
    // insertar" tiene una condición de carrera: dos peticiones concurrentes del mismo
    // usuario para la misma fecha pueden leer el "no existe" al mismo tiempo y ambas
    // intentar el INSERT, violando el índice único y fallando una de ellas (o dejando
    // datos inconsistentes si una gana en caché). ON CONFLICT (user_id, log_date) DO
    // UPDATE lo resuelve de forma atómica en la base: decide por nosotros si inserta o
    // actualiza sin ventana entre la lectura y la escritura.
    //
    // No basta con saber que la fila quedó escrita: necesita saber si se insertó o se
    // actualizó (p. ej. para estampar CreatedAt solo en la creación). ON CONFLICT no lo
    // distingue, así que se lee hidden system column xmax: su valor es el id de la
    // transacción que insertó la fila, y una fila recién insertada en esta transacción
    // tiene xmax = 0. (xmax = 0) AS was_inserted devuelve entonces true si esta sentencia
    // creó la fila y false si solo la actualizó.
    public async Task<UpsertedDailyLog> UpsertAsync(DailyLog log, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO daily_logs (
                id, user_id, log_date, mood_rating, anxiety_level, stress_level,
                sleep_hours, sleep_quality, sleep_disturbances, activity_type,
                activity_minutes, social_frequency, symptoms, notes, created_at, updated_at)
            VALUES (
                @id, @user_id, @log_date, @mood_rating, @anxiety_level, @stress_level,
                @sleep_hours, @sleep_quality, @sleep_disturbances, @activity_type,
                @activity_minutes, @social_frequency, @symptoms, @notes, @created_at, @updated_at)
            ON CONFLICT (user_id, log_date) DO UPDATE SET
                mood_rating = EXCLUDED.mood_rating,
                anxiety_level = EXCLUDED.anxiety_level,
                stress_level = EXCLUDED.stress_level,
                sleep_hours = EXCLUDED.sleep_hours,
                sleep_quality = EXCLUDED.sleep_quality,
                sleep_disturbances = EXCLUDED.sleep_disturbances,
                activity_type = EXCLUDED.activity_type,
                activity_minutes = EXCLUDED.activity_minutes,
                social_frequency = EXCLUDED.social_frequency,
                symptoms = EXCLUDED.symptoms,
                notes = EXCLUDED.notes,
                updated_at = EXCLUDED.updated_at
            RETURNING id, (xmax = 0) AS was_inserted;
            """;

        command.Parameters.Add(new NpgsqlParameter("@id", NpgsqlDbType.Uuid)
            { Value = log.Id == Guid.Empty ? Guid.NewGuid() : log.Id });
        command.Parameters.Add(new NpgsqlParameter("@user_id", NpgsqlDbType.Uuid) { Value = log.UserId });
        command.Parameters.Add(new NpgsqlParameter("@log_date", NpgsqlDbType.Date) { Value = log.LogDate });
        command.Parameters.Add(new NpgsqlParameter("@mood_rating", NpgsqlDbType.Smallint) { Value = log.MoodRating });
        command.Parameters.Add(new NpgsqlParameter("@anxiety_level", NpgsqlDbType.Smallint) { Value = log.AnxietyLevel });
        command.Parameters.Add(new NpgsqlParameter("@stress_level", NpgsqlDbType.Smallint) { Value = log.StressLevel });
        command.Parameters.Add(new NpgsqlParameter("@sleep_hours", NpgsqlDbType.Numeric) { Value = log.SleepHours });
        command.Parameters.Add(new NpgsqlParameter("@sleep_quality", NpgsqlDbType.Smallint) { Value = log.SleepQuality });

        var sleepDisturbances = new Converters.EnumListToStringArrayConverter<SleepDisturbance>()
            .ConvertToProvider(log.SleepDisturbances) as List<string>;
        command.Parameters.Add(new NpgsqlParameter("@sleep_disturbances", NpgsqlDbType.Array | NpgsqlDbType.Text)
            { Value = (object?)sleepDisturbances?.ToArray() ?? DBNull.Value });

        var activityType = new Converters.EnumSnakeCaseConverter<ActivityType>()
            .ConvertToProvider(log.ActivityType);
        command.Parameters.Add(new NpgsqlParameter("@activity_type", NpgsqlDbType.Text)
            { Value = (object?)activityType ?? DBNull.Value });

        command.Parameters.Add(new NpgsqlParameter("@activity_minutes", NpgsqlDbType.Smallint)
            { Value = (object?)log.ActivityMinutes ?? DBNull.Value });

        var socialFrequency = new Converters.EnumSnakeCaseConverter<SocialFrequency>()
            .ConvertToProvider(log.SocialFrequency);
        command.Parameters.Add(new NpgsqlParameter("@social_frequency", NpgsqlDbType.Text)
            { Value = socialFrequency });

        var symptoms = new Converters.JsonListConverter<Symptom>().ConvertToProvider(log.Symptoms);
        command.Parameters.Add(new NpgsqlParameter("@symptoms", NpgsqlDbType.Jsonb) { Value = symptoms });

        command.Parameters.Add(new NpgsqlParameter("@notes", NpgsqlDbType.Text) { Value = (object?)log.Notes ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("@created_at", NpgsqlDbType.TimestampTz) { Value = now });
        command.Parameters.Add(new NpgsqlParameter("@updated_at", NpgsqlDbType.TimestampTz) { Value = now });

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        var wasInserted = reader.GetBoolean(1);
        log.Id = reader.GetGuid(0);
        log.UpdatedAt = now;
        if (wasInserted)
        {
            log.CreatedAt = now;
        }

        return new UpsertedDailyLog(log, wasInserted);
    }
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

    }