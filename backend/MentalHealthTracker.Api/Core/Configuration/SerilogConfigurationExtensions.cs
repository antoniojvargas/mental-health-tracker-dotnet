using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace MentalHealthTracker.Api.Core.Configuration;

public static class SerilogConfigurationExtensions
{
    public static IHostBuilder UseSerilogLogging(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseSerilog((context, services, configuration) =>
            configuration
                .Destructure.With(new RedactSensitiveHeadersPolicy())
                .Enrich.FromLogContext()
                .MinimumLevel.Is(ReadMinimumLevel())
                .ConsoleFormatter(context.HostingEnvironment));
    }

    private static LoggerConfiguration ConsoleFormatter(
        this LoggerConfiguration configuration,
        IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            // CA1305: el Console de Serilog formatea con la cultura del proceso si no se le da un
            // formatProvider. Fijar InvariantCulture cambiaria la salida de la consola de un host con
            // otra cultura (decimales, fechas), asi que se deja como esta: es solo la consola de
            // desarrollo y el contenedor corre con cultura invariante.
#pragma warning disable CA1305
            return configuration.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{RequestId}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
#pragma warning restore CA1305
        }

        return configuration.WriteTo.Console(new CompactJsonFormatter());
    }

    private static LogEventLevel ReadMinimumLevel()
    {
        var raw = Environment.GetEnvironmentVariable("LOG_LEVEL");
        return Enum.TryParse(raw, ignoreCase: true, out LogEventLevel level)
            ? level
            : LogEventLevel.Information;
    }
}