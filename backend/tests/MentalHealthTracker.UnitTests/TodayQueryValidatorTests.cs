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
}
