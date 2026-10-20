using System.Text.Json;
using MentalHealthTracker.Domain.Common;
using MentalHealthTracker.Domain.Enums;

namespace MentalHealthTracker.UnitTests;

public sealed class SnakeCaseEnumTests
{
    // Equivalencia con la política que usaba el código anterior, para TODOS los miembros de
    // las cuatro enums del dominio: si se añade uno, entra solo en estas comprobaciones.
    [Fact]
    public void ToSnakeCase_MatchesTheJsonNamingPolicyForEveryMemberOfTheFourEnums()
    {
        AssertEquivalentToTheNamingPolicy<ActivityType>();
        AssertEquivalentToTheNamingPolicy<SleepDisturbance>();
        AssertEquivalentToTheNamingPolicy<SocialFrequency>();
        AssertEquivalentToTheNamingPolicy<SymptomType>();
    }

    [Fact]
    public void Parse_RoundTripsEveryMemberOfTheFourEnums()
    {
        AssertRoundTrips<ActivityType>();
        AssertRoundTrips<SleepDisturbance>();
        AssertRoundTrips<SocialFrequency>();
        AssertRoundTrips<SymptomType>();
    }

    // Nombres escritos a mano, para que renombrar un miembro rompa el contrato a la vista.
    [Fact]
    public void ToSnakeCase_ProducesTheHandWrittenWireNames()
    {
        Assert.Equal("frequent_waking", SnakeCaseEnum<SleepDisturbance>.ToSnakeCase(SleepDisturbance.FrequentWaking));
        Assert.Equal("appetite_change", SnakeCaseEnum<SymptomType>.ToSnakeCase(SymptomType.AppetiteChange));
        Assert.Equal("low_mood", SnakeCaseEnum<SymptomType>.ToSnakeCase(SymptomType.LowMood));
        Assert.Equal("walking", SnakeCaseEnum<ActivityType>.ToSnakeCase(ActivityType.Walking));
        Assert.Equal("occasional", SnakeCaseEnum<SocialFrequency>.ToSnakeCase(SocialFrequency.Occasional));
    }

    [Theory]
    [InlineData("frequent_waking")]
    [InlineData("FREQUENT_WAKING")]
    [InlineData("Frequent_Waking")]
    public void TryParse_IgnoresCase(string value)
    {
        Assert.True(SnakeCaseEnum<SleepDisturbance>.TryParse(value, out var parsed));
        Assert.Equal(SleepDisturbance.FrequentWaking, parsed);
    }

    [Theory]
    [InlineData("FrequentWaking")]
    [InlineData("frequent-waking")]
    [InlineData(" frequent_waking")]
    [InlineData("frequent_waking ")]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_RejectsAnythingThatIsNotASnakeCaseMemberName(string? value)
    {
        Assert.False(SnakeCaseEnum<SleepDisturbance>.TryParse(value, out var parsed));
        Assert.Equal(default, parsed);
    }

    [Fact]
    public void Parse_UnknownValue_ThrowsFormatExceptionNamingTheValueAndTheEnum()
    {
        var exception = Assert.Throws<FormatException>(() => SnakeCaseEnum<ActivityType>.Parse("swimming"));

        Assert.Contains("swimming", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ActivityType), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Null_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => SnakeCaseEnum<ActivityType>.Parse(null));
    }

    [Fact]
    public void ToSnakeCase_UndefinedValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SnakeCaseEnum<ActivityType>.ToSnakeCase((ActivityType)99));
    }

    private static void AssertEquivalentToTheNamingPolicy<TEnum>()
        where TEnum : struct, Enum
    {
        var values = Enum.GetValues<TEnum>();
        Assert.NotEmpty(values);

        foreach (var value in values)
        {
            Assert.Equal(
                JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString()),
                SnakeCaseEnum<TEnum>.ToSnakeCase(value));
        }
    }

    private static void AssertRoundTrips<TEnum>()
        where TEnum : struct, Enum
    {
        foreach (var value in Enum.GetValues<TEnum>())
        {
            var name = SnakeCaseEnum<TEnum>.ToSnakeCase(value);

            Assert.Equal(value, SnakeCaseEnum<TEnum>.Parse(name));
            Assert.True(SnakeCaseEnum<TEnum>.TryParse(name, out var parsed));
            Assert.Equal(value, parsed);
        }
    }
}
