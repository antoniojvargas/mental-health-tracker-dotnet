using MentalHealthTracker.Api.Core.Health;

namespace MentalHealthTracker.UnitTests;

public sealed class ApplicationUptimeTests
{
    [Fact]
    public void ApplicationUptime_CountsFromTheInstantItWasCreated()
    {
        var clock = new FixedTimeProvider();
        var uptime = new ApplicationUptime(clock);

        clock.Advance(TimeSpan.FromSeconds(90));

        Assert.Equal(FixedTimeProvider.NoonUtc, uptime.StartedAt);
        Assert.Equal(TimeSpan.FromSeconds(90), uptime.Elapsed);
    }
}
