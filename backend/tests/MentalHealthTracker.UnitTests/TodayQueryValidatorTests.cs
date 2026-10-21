using System.Globalization;
using MentalHealthTracker.Api.Modules.DailyLog.Dtos;
using MentalHealthTracker.Api.Modules.DailyLog.Validators;

namespace MentalHealthTracker.UnitTests;

public class TodayQueryValidatorTests
{
    [Fact]
    public void WithoutDate_IsValid()
    {
        var validator = new TodayQueryValidator(new FixedTimeProvider());

        Assert.True(validator.Validate(new TodayQuery()).IsValid);
    }

    // Mismo límite que al crear un registro: la fecha UTC + 1 día (UTC+14 es la zona más
    // adelantada), que se desplaza un día al cruzar la medianoche UTC.
    [Theory]
    [InlineData("2030-06-15T23:59:59.9999999+00:00", "2030-06-16", true)]
    [InlineData("2030-06-15T23:59:59.9999999+00:00", "2030-06-17", false)]
    [InlineData("2030-06-16T00:00:00+00:00", "2030-06-16", true)]
    [InlineData("2030-06-16T00:00:00+00:00", "2030-06-17", true)]
    [InlineData("2030-06-16T00:00:00+00:00", "2030-06-18", false)]
    public void Date_Limit_MovesAtUtcMidnight(string now, string date, bool expectedValid)
    {
        var validator = new TodayQueryValidator(
            new FixedTimeProvider(DateTimeOffset.Parse(now, CultureInfo.InvariantCulture)));

        var result = validator.Validate(new TodayQuery(DateOnly.Parse(date, CultureInfo.InvariantCulture)));

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void FutureDate_FailsOnTheDatePropertyWithTheLimitInTheMessage()
    {
        var validator = new TodayQueryValidator(new FixedTimeProvider());

        var result = validator.Validate(new TodayQuery(new DateOnly(2030, 6, 20)));

        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(TodayQuery.Date), error.PropertyName);
        Assert.Equal("date cannot be more than 1 day ahead of the current UTC date.", error.ErrorMessage);
    }

    [Theory]
    [InlineData("2030-06-15")]
    [InlineData("2030-06-14")]
    [InlineData("2020-01-01")]
    [InlineData("0001-01-01")]
    public void PastOrCurrentDates_AreAlwaysValid(string date)
    {
        var validator = new TodayQueryValidator(new FixedTimeProvider());

        Assert.True(validator.Validate(new TodayQuery(DateOnly.Parse(date, CultureInfo.InvariantCulture))).IsValid);
    }

    [Fact]
    public void MaxDate_IsRejectedWithoutOverflowing()
    {
        var validator = new TodayQueryValidator(new FixedTimeProvider());

        Assert.False(validator.Validate(new TodayQuery(DateOnly.MaxValue)).IsValid);
    }

    [Fact]
    public void FollowsTheClockOfTheInjectedTimeProvider()
    {
        var clock = new FixedTimeProvider();
        var validator = new TodayQueryValidator(clock);
        var query = new TodayQuery(new DateOnly(2030, 6, 17));

        Assert.False(validator.Validate(query).IsValid);

        clock.Advance(TimeSpan.FromDays(1));

        Assert.True(validator.Validate(query).IsValid);
    }
}
