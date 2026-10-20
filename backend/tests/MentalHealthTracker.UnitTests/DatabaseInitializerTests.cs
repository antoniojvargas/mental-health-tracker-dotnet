using MentalHealthTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MentalHealthTracker.UnitTests;

// El arranque debe REINTENTAR cuando la base no está disponible (p. ej. el contenedor de
// Postgres todavía no resuelve por DNS), no morir con la excepción de red. Según la versión
// de Npgsql, un host que no resuelve llega envuelto en NpgsqlException o como SocketException
// a secas; los dos casos tienen que acabar en el bucle de reintento.
public sealed class DatabaseInitializerTests
{
    [Fact]
    public async Task MigrateAsync_WhenTheHostDoesNotResolve_KeepsRetryingInsteadOfCrashing()
    {
        await using var dbContext = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=db-that-does-not-exist.invalid;Port=5432;Database=x;Username=x;Password=x;Timeout=2")
                .Options);
        using var cancellation = new CancellationTokenSource();
        var logger = new CapturingLogger();

        // La resolución de DNS tarda lo que tarde cada entorno, así que no se cancela por
        // tiempo: en cuanto se registra el aviso del primer intento se corta la espera
        // (1 s) antes del segundo. El tope de abajo solo evita un test colgado.
        logger.OnWarning = cancellation.Cancel;
        cancellation.CancelAfter(TimeSpan.FromSeconds(60));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => DatabaseInitializer.MigrateAsync(dbContext, logger, cancellation.Token));

        var retry = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("Database not ready (attempt 1/10)", retry.Message);
        Assert.NotNull(retry.Exception);
        Assert.Equal((2003, "DatabaseNotReady"), (retry.EventId.Id, retry.EventId.Name));
    }

    // Una base que no contesta (timeout al conectar) también es "todavía no está lista": EF
    // envuelve ese TimeoutException en una InvalidOperationException ("transient failure") y el
    // arranque debe seguir reintentando, no morir. Es el caso que se vio en CI, donde la
    // resolución de DNS del test anterior llegaba como timeout en lugar de como error de red.
    [Fact]
    public async Task MigrateAsync_WhenTheDatabaseTimesOutOnConnect_KeepsRetryingInsteadOfCrashing()
    {
        await using var dbContext = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=10.255.255.1;Port=5432;Database=x;Username=x;Password=x;Timeout=1;Command Timeout=1")
                .Options);
        using var cancellation = new CancellationTokenSource();
        var logger = new CapturingLogger { OnWarning = cancellation.Cancel };
        cancellation.CancelAfter(TimeSpan.FromSeconds(60));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => DatabaseInitializer.MigrateAsync(dbContext, logger, cancellation.Token));

        var retry = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("Database not ready (attempt 1/10)", retry.Message);
    }

    public static TheoryData<Exception, bool> NotReadyCases => new()
    {
        { new NpgsqlException("down"), true },
        { new TimeoutException(), true },
        { new System.Net.Sockets.SocketException(), true },
        { new InvalidOperationException("transient", new TimeoutException()), true },
        { new InvalidOperationException("outer", new InvalidOperationException("inner", new NpgsqlException("down"))), true },
        { new InvalidOperationException("no cause"), false },
        { new InvalidOperationException("unrelated cause", new ArgumentException("x")), false },
        { new ArgumentException("x"), false },
    };

    [Theory]
    [MemberData(nameof(NotReadyCases))]
    public void IsDatabaseNotReady_OnlyRetriesAvailabilityProblems(Exception exception, bool expected)
    {
        Assert.Equal(expected, DatabaseInitializer.IsDatabaseNotReady(exception));
    }

    private sealed record Entry(LogLevel Level, string Message, Exception? Exception, EventId EventId);

    private sealed class CapturingLogger : ILogger
    {
        public List<Entry> Entries { get; } = [];

        public Action? OnWarning { get; set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new Entry(logLevel, formatter(state, exception), exception, eventId));
            if (logLevel == LogLevel.Warning)
            {
                OnWarning?.Invoke();
            }
        }
    }
}
