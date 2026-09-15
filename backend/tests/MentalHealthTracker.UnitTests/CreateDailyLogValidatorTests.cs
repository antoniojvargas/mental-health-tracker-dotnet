using MentalHealthTracker.Api.Modules.DailyLog.Dtos;
using MentalHealthTracker.Api.Modules.DailyLog.Validators;
using MentalHealthTracker.Domain.Enums;
using MentalHealthTracker.Domain.ValueObjects;

namespace MentalHealthTracker.UnitTests;

public class CreateDailyLogValidatorTests
{
    private readonly CreateDailyLogValidator _validator = new();

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void MoodRating_Boundaries_AreValidated(int value, bool expectedValid)
    {
        var request = ValidRequest() with { MoodRating = (short)value };

        var result = _validator.Validate(request);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void AnxietyLevel_Boundaries_AreValidated(int value, bool expectedValid)
    {
        var request = ValidRequest() with { AnxietyLevel = (short)value };

        var result = _validator.Validate(request);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void StressLevel_Boundaries_AreValidated(int value, bool expectedValid)
    {
        var request = ValidRequest() with { StressLevel = (short)value };

        var result = _validator.Validate(request);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(24, true)]
    [InlineData(25, false)]
    public void SleepHours_Boundaries_AreValidated(int value, bool expectedValid)
    {
        var request = ValidRequest() with { SleepHours = value };

        var result = _validator.Validate(request);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void SleepQuality_Boundaries_AreValidated(int value, bool expectedValid)
    {
        var request = ValidRequest() with { SleepQuality = (short)value };

        var result = _validator.Validate(request);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(600, true)]
    [InlineData(601, false)]
    public void ActivityMinutes_Boundaries_AreValidated(int value, bool expectedValid)
    {
        var request = ValidRequest() with { ActivityMinutes = (short)value };

        var result = _validator.Validate(request);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void ActivityMinutes_WhenNull_IsValid()
    {
        var request = ValidRequest() with { ActivityMinutes = null };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Notes_WithExactlyOneThousandCharacters_IsValid()
    {
        var request = ValidRequest() with { Notes = new string('a', 1000) };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Notes_WithMoreThanOneThousandCharacters_IsInvalid()
    {
        var request = ValidRequest() with { Notes = new string('a', 1001) };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void SymptomsSeverity_Boundaries_AreValidated(int value, bool expectedValid)
    {
        var request = ValidRequest() with { Symptoms = [new Symptom(SymptomType.Fatigue, value)] };

        var result = _validator.Validate(request);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void LogDate_InTheFuture_HasExplicitErrorMessage()
    {
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var request = ValidRequest(futureDate);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.PropertyName == nameof(CreateDailyLogRequest.LogDate) &&
            error.ErrorMessage == "logDate no puede estar en el futuro.");
    }

    private static CreateDailyLogRequest ValidRequest(DateOnly? logDate = null) => new(
        LogDate: logDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
        MoodRating: 3,
        AnxietyLevel: 5,
        StressLevel: 5,
        SleepHours: 7.5m,
        SleepQuality: 4,
        SleepDisturbances: [],
        ActivityType: ActivityType.Walking,
        ActivityMinutes: 30,
        SocialFrequency: SocialFrequency.Occasional,
        Symptoms: [new Symptom(SymptomType.Fatigue, 3)],
        Notes: null);
}