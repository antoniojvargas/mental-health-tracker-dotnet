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
                .MinimumLevel.Is(ReadMinimumLevel())
                .ConsoleFormatter(context.HostingEnvironment));
    }

    private static LoggerConfiguration ConsoleFormatter(
        this LoggerConfiguration configuration,
        IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            return configuration.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
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