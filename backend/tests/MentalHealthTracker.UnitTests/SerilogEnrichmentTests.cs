using MentalHealthTracker.Api.Core.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace MentalHealthTracker.UnitTests;

// Application y Environment deben salir en cada evento, vengan de donde vengan.
public sealed class SerilogEnrichmentTests
{
    [Fact]
    public void EveryEvent_CarriesTheApplicationAndTheEnvironment()
    {
        var sink = new ListSink();
        var logger = new LoggerConfiguration()
            .EnrichWithApplication(new FakeEnvironment("MentalHealthTracker.Api", "Staging"))
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("one");
        logger.Warning("two");

        Assert.Equal(2, sink.Events.Count);
        Assert.All(sink.Events, e =>
        {
            Assert.Equal("\"MentalHealthTracker.Api\"", e.Properties["Application"].ToString());
            Assert.Equal("\"Staging\"", e.Properties["Environment"].ToString());
        });
    }

    private sealed class ListSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private sealed class FakeEnvironment(string applicationName, string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = applicationName;

        public string ContentRootPath { get; set; } = string.Empty;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
