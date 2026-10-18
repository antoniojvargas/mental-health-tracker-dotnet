using MentalHealthTracker.Api.Core.Health;
using MentalHealthTracker.Api.Modules.DailyLog.Dtos;
using MentalHealthTracker.Api.Modules.DailyLog.Validators;

namespace MentalHealthTracker.UnitTests;

public sealed class TimeProviderConsumersTests
{
    private static readonly DateTimeOffset Start = new(2030, 6, 15, 23, 59, 59, TimeSpan.Zero);

    [Fact]
    public void TodayQueryValidator_Limit_FollowsTheInjectedClock()
    {
        var clock = new FixedTimeProvider(Start);
        var validator = new TodayQueryValidator(clock);
        var dayAfterTomorrow = new TodayQuery(new DateOnly(2030, 6, 17));

        Assert.True(validator.Validate(new TodayQuery(new DateOnly(2030, 6, 16))).IsValid);
        Assert.False(validator.Validate(dayAfterTomorrow).IsValid);

        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.True(validator.Validate(dayAfterTomorrow).IsValid);
    }

    [Fact]
    public void ApplicationUptime_CountsFromTheInstantItWasCreated()
    {
        var clock = new FixedTimeProvider(Start);
        var uptime = new ApplicationUptime(clock);

        clock.Advance(TimeSpan.FromSeconds(90));

        Assert.Equal(Start, uptime.StartedAt);
        Assert.Equal(TimeSpan.FromSeconds(90), uptime.Elapsed);
    }
}
