using MentalHealthTracker.Domain.Entities;
using MentalHealthTracker.Domain.Enums;
using MentalHealthTracker.Domain.Models;
using MentalHealthTracker.Domain.Repositories;
using MentalHealthTracker.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace MentalHealthTracker.Infrastructure.Persistence.Repositories;

public sealed class DailyLogRepository(AppDbContext dbContext, TimeProvider timeProvider) : IDailyLogRepository
{
    // Los conversores no tienen estado y sus delegados son seguros entre hilos: se crean una
    // vez, no en cada upsert.
    private static readonly Converters.EnumListToStringArrayConverter<SleepDisturbance> SleepDisturbancesConverter = new();

    private static readonly Converters.EnumSnakeCaseConverter<ActivityType> ActivityTypeConverter = new();

    private static readonly Converters.EnumSnakeCaseConverter<SocialFrequency> SocialFrequencyConverter = new();

    private static readonly Converters.JsonListConverter<Symptom> SymptomsConverter = new();

    // La sentencia del upsert (ver el comentario de UpsertAsync); los parámetros los añade
    // AddUpsertParameters.
    private const string UpsertSql = """
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
        var now = timeProvider.GetUtcNow();
        bool wasInserted;

        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = UpsertSql;
        AddUpsertParameters(command, log, now);

        // OpenConnectionAsync/CloseConnectionAsync llevan la cuenta de EF: si la conexión ya estaba
        // abierta (otra operación del contexto o una transacción) no la cierran; si la abrió esta
        // llamada, la cierra el finally. Y el comando se enlista en la transacción del contexto,
        // para que un rollback también deshaga el upsert.
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);

            wasInserted = reader.GetBoolean(1);
            log.Id = reader.GetGuid(0);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }

        log.UpdatedAt = now;
        if (wasInserted)
        {
            log.CreatedAt = now;
        }

        return new UpsertedDailyLog(log, wasInserted);
    }

    private static void AddUpsertParameters(System.Data.Common.DbCommand command, DailyLog log, DateTimeOffset now)
    {
        command.Parameters.Add(new NpgsqlParameter("@id", NpgsqlDbType.Uuid)
        { Value = log.Id == Guid.Empty ? Guid.NewGuid() : log.Id });
        command.Parameters.Add(new NpgsqlParameter("@user_id", NpgsqlDbType.Uuid) { Value = log.UserId });
        command.Parameters.Add(new NpgsqlParameter("@log_date", NpgsqlDbType.Date) { Value = log.LogDate });
        command.Parameters.Add(new NpgsqlParameter("@mood_rating", NpgsqlDbType.Smallint) { Value = log.MoodRating });
        command.Parameters.Add(new NpgsqlParameter("@anxiety_level", NpgsqlDbType.Smallint) { Value = log.AnxietyLevel });
        command.Parameters.Add(new NpgsqlParameter("@stress_level", NpgsqlDbType.Smallint) { Value = log.StressLevel });
        command.Parameters.Add(new NpgsqlParameter("@sleep_hours", NpgsqlDbType.Numeric) { Value = log.SleepHours });
        command.Parameters.Add(new NpgsqlParameter("@sleep_quality", NpgsqlDbType.Smallint) { Value = log.SleepQuality });

        var sleepDisturbances = SleepDisturbancesConverter
            .ConvertToProvider(log.SleepDisturbances) as List<string>;
        command.Parameters.Add(new NpgsqlParameter("@sleep_disturbances", NpgsqlDbType.Array | NpgsqlDbType.Text)
        { Value = (object?)sleepDisturbances?.ToArray() ?? DBNull.Value });

        var activityType = ActivityTypeConverter
            .ConvertToProvider(log.ActivityType);
        command.Parameters.Add(new NpgsqlParameter("@activity_type", NpgsqlDbType.Text)
        { Value = (object?)activityType ?? DBNull.Value });

        command.Parameters.Add(new NpgsqlParameter("@activity_minutes", NpgsqlDbType.Smallint)
        { Value = (object?)log.ActivityMinutes ?? DBNull.Value });

        var socialFrequency = SocialFrequencyConverter
            .ConvertToProvider(log.SocialFrequency);
        command.Parameters.Add(new NpgsqlParameter("@social_frequency", NpgsqlDbType.Text)
        { Value = socialFrequency });

        var symptoms = SymptomsConverter.ConvertToProvider(log.Symptoms);
        command.Parameters.Add(new NpgsqlParameter("@symptoms", NpgsqlDbType.Jsonb) { Value = symptoms });

        command.Parameters.Add(new NpgsqlParameter("@notes", NpgsqlDbType.Text) { Value = (object?)log.Notes ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("@created_at", NpgsqlDbType.TimestampTz) { Value = now });
        command.Parameters.Add(new NpgsqlParameter("@updated_at", NpgsqlDbType.TimestampTz) { Value = now });
    }

    public async Task<DailyLog?> FindByUserAndDateAsync(Guid userId, DateOnly logDate, CancellationToken cancellationToken = default)
        => await dbContext.DailyLogs
            .AsNoTracking()
            .SingleOrDefaultAsync(log => log.UserId == userId && log.LogDate == logDate, cancellationToken);

    // La página y el total del rango salen de UNA consulta: COUNT(*) OVER() se calcula sobre el
    // conjunto filtrado ANTES de OFFSET/LIMIT, así que cada fila de la página trae el total.
    // Si la página llega vacía (offset fuera de rango) no hay fila que lo lleve y se pide con un
    // COUNT de respaldo, para que el total sea el mismo en todos los casos. Con offset 0 y un
    // límite positivo una página vacía solo puede significar rango vacío: el total es 0 sin
    // segundo viaje.
    public async Task<PagedResult<DailyLog>> FindByUserAndRangeAsync(
        Guid userId,
        DateOnly fromDate,
        DateOnly toDate,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        var items = new List<DailyLog>();
        var total = 0L;

        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = RangePageSql;
        command.Parameters.Add(new NpgsqlParameter("@user_id", NpgsqlDbType.Uuid) { Value = userId });
        command.Parameters.Add(new NpgsqlParameter("@from_date", NpgsqlDbType.Date) { Value = fromDate });
        command.Parameters.Add(new NpgsqlParameter("@to_date", NpgsqlDbType.Date) { Value = toDate });
        command.Parameters.Add(new NpgsqlParameter("@limit", NpgsqlDbType.Integer) { Value = limit });
        command.Parameters.Add(new NpgsqlParameter("@offset", NpgsqlDbType.Integer) { Value = offset });

        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(ReadLog(reader));
                total = reader.GetInt64(RangePageTotalColumn);
            }
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }

        if (items.Count == 0 && (offset > 0 || limit <= 0))
        {
            total = await dbContext.DailyLogs
                .AsNoTracking()
                .CountAsync(log => log.UserId == userId && log.LogDate >= fromDate && log.LogDate <= toDate, cancellationToken);
        }

        return new PagedResult<DailyLog>(items, (int)total);
    }

    // Misma proyección que la tabla, en el orden de ReadLog, más el total de la ventana al final.
    private const string RangePageSql = """
        SELECT id, user_id, log_date, mood_rating, anxiety_level, stress_level,
               sleep_hours, sleep_quality, sleep_disturbances, activity_type,
               activity_minutes, social_frequency, symptoms, notes, created_at, updated_at,
               COUNT(*) OVER() AS total
        FROM daily_logs
        WHERE user_id = @user_id AND log_date >= @from_date AND log_date <= @to_date
        ORDER BY log_date
        OFFSET @offset LIMIT @limit;
        """;

    private const int RangePageTotalColumn = 16;

    // Materializa una fila de RangePageSql aplicando la misma conversión (snake_case, text[],
    // jsonb) que los conversores de EF, sin pasar por el seguimiento de cambios.
    private static DailyLog ReadLog(System.Data.Common.DbDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        UserId = reader.GetGuid(1),
        LogDate = reader.GetFieldValue<DateOnly>(2),
        MoodRating = reader.GetInt16(3),
        AnxietyLevel = reader.GetInt16(4),
        StressLevel = reader.GetInt16(5),
        SleepHours = reader.GetDecimal(6),
        SleepQuality = reader.GetInt16(7),
        SleepDisturbances = reader.GetFieldValue<string[]>(8)
            .Select(Converters.ParseStored<SleepDisturbance>)
            .ToList(),
        ActivityType = reader.IsDBNull(9) ? null : Converters.ParseStored<ActivityType>(reader.GetString(9)),
        ActivityMinutes = reader.IsDBNull(10) ? null : reader.GetInt16(10),
        SocialFrequency = Converters.ParseStored<SocialFrequency>(reader.GetString(11)),
        Symptoms = (List<Symptom>)SymptomsConverter.ConvertFromProvider(reader.GetString(12))!,
        Notes = reader.IsDBNull(13) ? null : reader.GetString(13),
        CreatedAt = reader.GetFieldValue<DateTimeOffset>(14),
        UpdatedAt = reader.GetFieldValue<DateTimeOffset>(15),
    };
}
