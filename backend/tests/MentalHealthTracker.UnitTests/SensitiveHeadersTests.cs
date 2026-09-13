using System.Text;
using MentalHealthTracker.Api.Core.Configuration;
using Microsoft.Extensions.Primitives;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact;

namespace MentalHealthTracker.UnitTests;

public class SensitiveHeadersTests
{
    private const string CookieValue = "session=super-secret-cookie-value";
    private const string AuthorizationValue = "Bearer secret-token-abc";

    [Fact]
    public void RequestLog_DoesNotContainCookieValue_EvenAtDebugLevel()
    {
        var sink = new StringSink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Destructure.With(new RedactSensitiveHeadersPolicy())
            .WriteTo.Sink(sink)
            .CreateLogger();

        var headers = new Dictionary<string, StringValues>
        {
            ["Cookie"] = CookieValue,
            ["Authorization"] = AuthorizationValue,
            ["X-Request-Id"] = "req-123",
        };

        logger.Information("Request headers {@Headers} {RequestId}", headers, "req-123");

        var output = sink.ToString();

        Assert.DoesNotContain(CookieValue, output);
        Assert.DoesNotContain(AuthorizationValue, output);
        Assert.Contains("REDACTED", output);
        Assert.Contains("req-123", output);
    }

    private sealed class StringSink : ILogEventSink
    {
        private readonly StringBuilder _builder = new();
        private readonly ITextFormatter _formatter = new CompactJsonFormatter();

        public void Emit(LogEvent logEvent)
        {
            using var writer = new StringWriter(_builder);
            _formatter.Format(logEvent, writer);
        }

        public override string ToString() => _builder.ToString();
    }
}