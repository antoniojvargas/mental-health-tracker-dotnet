using Microsoft.Extensions.Primitives;
using Serilog.Core;
using Serilog.Events;

namespace MentalHealthTracker.Api.Core.Configuration;

public sealed class RedactSensitiveHeadersPolicy : IDestructuringPolicy
{
    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cookie",
        "Set-Cookie",
        "Authorization",
    };

    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        out LogEventPropertyValue result)
    {
        if (value is not IDictionary<string, StringValues> headers)
        {
            result = null!;
            return false;
        }

        var properties = new List<LogEventProperty>(headers.Count);
        foreach (var header in headers)
        {
            var redacted = SensitiveHeaders.Contains(header.Key);
            var headerValue = redacted
                ? new ScalarValue("[REDACTED]")
                : new ScalarValue(header.Value.ToString());

            properties.Add(new LogEventProperty(header.Key, headerValue));
        }

        result = new StructureValue(properties);
        return true;
    }
}