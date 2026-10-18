using MentalHealthTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
        var logger = new CapturingLogger();
        using var cancellation = new CancellationTokenSource();

        // El primer intento falla enseguida; la espera de 1 s antes del segundo se cancela.
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(600));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => DatabaseInitializer.MigrateAsync(dbContext, logger, cancellation.Token));

        var retry = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("Database not ready (attempt 1/10)", retry.Message);
        Assert.NotNull(retry.Exception);
    }

    private sealed record Entry(LogLevel Level, string Message, Exception? Exception);

    private sealed class CapturingLogger : ILogger
    {
        public List<Entry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new Entry(logLevel, formatter(state, exception), exception));
    }
}
