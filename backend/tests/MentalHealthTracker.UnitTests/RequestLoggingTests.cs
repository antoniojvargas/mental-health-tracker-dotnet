using MentalHealthTracker.Api.Core.Logging;
using Serilog.Events;

namespace MentalHealthTracker.UnitTests;

public sealed class RequestLoggingTests
{
    [Theory]
    [InlineData(200, LogEventLevel.Information)]
    [InlineData(201, LogEventLevel.Information)]
    [InlineData(204, LogEventLevel.Information)]
    [InlineData(302, LogEventLevel.Information)]
    [InlineData(399, LogEventLevel.Information)]
    [InlineData(400, LogEventLevel.Warning)]
    [InlineData(401, LogEventLevel.Warning)]
    [InlineData(404, LogEventLevel.Warning)]
    [InlineData(429, LogEventLevel.Warning)]
    [InlineData(499, LogEventLevel.Warning)]
    [InlineData(500, LogEventLevel.Error)]
    [InlineData(503, LogEventLevel.Error)]
    public void LevelFor_FollowsTheStatusClass(int status, LogEventLevel expected)
    {
        Assert.Equal(expected, RequestLogging.LevelFor(exception: null, status));
    }

    [Theory]
    [InlineData(200)]
    [InlineData(404)]
    [InlineData(500)]
    public void LevelFor_AnExceptionThatEscapedIsAlwaysAnError(int status)
    {
        Assert.Equal(LogEventLevel.Error, RequestLogging.LevelFor(new InvalidOperationException("boom"), status));
    }
}
