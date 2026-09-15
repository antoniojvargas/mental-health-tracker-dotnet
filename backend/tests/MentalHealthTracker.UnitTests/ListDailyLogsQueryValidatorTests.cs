using MentalHealthTracker.Api.Modules.DailyLog.Dtos;
using MentalHealthTracker.Api.Modules.DailyLog.Validators;

namespace MentalHealthTracker.UnitTests;

public class ListDailyLogsQueryValidatorTests
{
    private readonly ListDailyLogsQueryValidator _validator = new();

    [Theory]
    [InlineData(1, true)]
    [InlineData(366, true)]
    [InlineData(367, false)]
    public void Limit_Boundaries_AreValidated(int value, bool expectedValid)
    {
        var query = ValidQuery() with { Limit = value };

        var result = _validator.Validate(query);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void Limit_WithDefaultValue_IsValid()
    {
        var query = ValidQuery() with { Limit = 100 };

        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, false)]
    public void Offset_Boundaries_AreValidated(int value, bool expectedValid)
    {
        var query = ValidQuery() with { Offset = value };

        var result = _validator.Validate(query);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData(365, true)]
    [InlineData(366, true)]
    [InlineData(367, false)]
    public void RangeDays_Boundaries_AreValidated(int days, bool expectedValid)
    {
        var from = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = ValidQuery() with { From = from, To = from.AddDays(days) };

        var result = _validator.Validate(query);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void From_EqualTo_IsValid()
    {
        var from = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = ValidQuery() with { From = from, To = from };

        var result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void InvertedRange_HasExplicitErrorMessage()
    {
        var to = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = ValidQuery() with { From = to.AddDays(1), To = to };

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.PropertyName == nameof(ListDailyLogsQuery.From) &&
            error.ErrorMessage == "from debe ser menor o igual que to.");
    }

    private static ListDailyLogsQuery ValidQuery()
    {
        var from = DateOnly.FromDateTime(DateTime.UtcNow);
        return new ListDailyLogsQuery(From: from, To: from.AddDays(30));
    }
}