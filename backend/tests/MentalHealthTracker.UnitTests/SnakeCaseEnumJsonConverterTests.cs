using System.Text.Json;
using MentalHealthTracker.Api.Core.Serialization;
using MentalHealthTracker.Domain.Enums;

namespace MentalHealthTracker.UnitTests;

// Pruebas de caracterización de SnakeCaseEnumJsonConverter: el contrato de las cuatro
// enums que viajan por la API. Los strings esperados están escritos a mano (no derivados
// del propio convertidor) para que renombrar un miembro de la enum rompa el contrato a la
// vista. El convertidor ya no va en un atributo de las enums (el dominio no sabe de JSON): se
// pasa en las opciones, igual que lo registra la API.
public sealed class SnakeCaseEnumJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new SnakeCaseEnumJsonConverter() },
    };

    public static TheoryData<ActivityType, string> ActivityTypes => new()
    {
        { ActivityType.None, "none" },
        { ActivityType.Walking, "walking" },
        { ActivityType.Running, "running" },
        { ActivityType.Gym, "gym" },
        { ActivityType.Yoga, "yoga" },
        { ActivityType.Cycling, "cycling" },
        { ActivityType.Sports, "sports" },
        { ActivityType.Other, "other" },
    };

    public static TheoryData<SleepDisturbance, string> SleepDisturbances => new()
    {
        { SleepDisturbance.None, "none" },
        { SleepDisturbance.Insomnia, "insomnia" },
        { SleepDisturbance.Nightmares, "nightmares" },
        { SleepDisturbance.FrequentWaking, "frequent_waking" },
        { SleepDisturbance.EarlyWaking, "early_waking" },
    };

    public static TheoryData<SocialFrequency, string> SocialFrequencies => new()
    {
        { SocialFrequency.None, "none" },
        { SocialFrequency.Rare, "rare" },
        { SocialFrequency.Occasional, "occasional" },
        { SocialFrequency.Frequent, "frequent" },
        { SocialFrequency.Daily, "daily" },
    };

    public static TheoryData<SymptomType, string> SymptomTypes => new()
    {
        { SymptomType.LowMood, "low_mood" },
        { SymptomType.Hopelessness, "hopelessness" },
        { SymptomType.Fatigue, "fatigue" },
        { SymptomType.Irritability, "irritability" },
        { SymptomType.Panic, "panic" },
        { SymptomType.Restlessness, "restlessness" },
        { SymptomType.Concentration, "concentration" },
        { SymptomType.AppetiteChange, "appetite_change" },
    };

    [Theory]
    [MemberData(nameof(ActivityTypes))]
    public void ActivityType_RoundTripsWithExactString(ActivityType value, string text) =>
        AssertRoundTrip(value, text);

    [Theory]
    [MemberData(nameof(SleepDisturbances))]
    public void SleepDisturbance_RoundTripsWithExactString(SleepDisturbance value, string text) =>
        AssertRoundTrip(value, text);

    [Theory]
    [MemberData(nameof(SocialFrequencies))]
    public void SocialFrequency_RoundTripsWithExactString(SocialFrequency value, string text) =>
        AssertRoundTrip(value, text);

    [Theory]
    [MemberData(nameof(SymptomTypes))]
    public void SymptomType_RoundTripsWithExactString(SymptomType value, string text) =>
        AssertRoundTrip(value, text);

    [Fact]
    public void TheTablesCoverEveryMemberOfEachEnum()
    {
        Assert.Equal(Enum.GetValues<ActivityType>().Length, ActivityTypes.Count);
        Assert.Equal(Enum.GetValues<SleepDisturbance>().Length, SleepDisturbances.Count);
        Assert.Equal(Enum.GetValues<SocialFrequency>().Length, SocialFrequencies.Count);
        Assert.Equal(Enum.GetValues<SymptomType>().Length, SymptomTypes.Count);
    }

    [Theory]
    [InlineData("\"FREQUENT_WAKING\"")]
    [InlineData("\"Frequent_Waking\"")]
    [InlineData("\"frequent_waking\"")]
    public void Read_IsCaseInsensitive(string json)
    {
        Assert.Equal(SleepDisturbance.FrequentWaking, JsonSerializer.Deserialize<SleepDisturbance>(json, Options));
    }

    [Fact]
    public void Read_IsCaseInsensitiveForEveryEnum()
    {
        Assert.Equal(SymptomType.AppetiteChange, JsonSerializer.Deserialize<SymptomType>("\"APPETITE_CHANGE\"", Options));
        Assert.Equal(ActivityType.Walking, JsonSerializer.Deserialize<ActivityType>("\"WALKING\"", Options));
        Assert.Equal(SocialFrequency.Daily, JsonSerializer.Deserialize<SocialFrequency>("\"Daily\"", Options));
    }

    [Theory]
    [InlineData("\"frequentwaking\"")]
    [InlineData("\"FrequentWaking\"")]
    [InlineData("\"frequent-waking\"")]
    [InlineData("\"frequent waking\"")]
    [InlineData("\" frequent_waking\"")]
    [InlineData("\"\"")]
    [InlineData("\"unknown\"")]
    public void Read_UnknownValue_ThrowsJsonException(string json)
    {
        // El PascalCase de la enum y otras variantes no son equivalentes: solo cuenta el
        // snake_case, comparado sin distinguir mayúsculas.
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SleepDisturbance>(json, Options));
    }

    [Fact]
    public void Read_UnknownValue_ThrowsJsonExceptionForEveryEnum()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ActivityType>("\"swimming\"", Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SocialFrequency>("\"sometimes\"", Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SymptomType>("\"sadness\"", Options));
    }

    [Fact]
    public void Read_Null_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SleepDisturbance>("null", Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SymptomType>("null", Options));
    }

    // Los mensajes forman parte del contrato de error: llegan al cliente dentro del 400.
    [Fact]
    public void Read_UnknownValue_HasTheExactMessage()
    {
        var exception = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SleepDisturbance>("\"unknown\"", Options));

        Assert.Equal("Invalid value 'unknown' for enum SleepDisturbance.", exception.Message);
    }

    [Fact]
    public void Read_Null_HasTheExactMessage()
    {
        var exception = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SymptomType>("null", Options));

        Assert.Equal("Invalid value for enum SymptomType: expected a string.", exception.Message);
    }

    [Fact]
    public void Read_NullInsideAnObject_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Holder>("""{"Activity":null}""", Options));
    }

    [Fact]
    public void Read_NullForNullableEnum_YieldsNull()
    {
        // Con el tipo anulable el serializador resuelve el null sin llamar al convertidor.
        Assert.Null(JsonSerializer.Deserialize<ActivityType?>("null", Options));
    }

    [Fact]
    public void Write_UsesTheSameStringsInsideAnObjectAndAList()
    {
        var json = JsonSerializer.Serialize(new Holder(ActivityType.Cycling, [SleepDisturbance.EarlyWaking]), Options);

        Assert.Equal("""{"Activity":"cycling","Sleep":["early_waking"]}""", json);
    }

    private static void AssertRoundTrip<T>(T value, string text)
        where T : struct, Enum
    {
        var json = JsonSerializer.Serialize(value, Options);

        Assert.Equal($"\"{text}\"", json);
        Assert.Equal(value, JsonSerializer.Deserialize<T>(json, Options));
    }

    private sealed record Holder(ActivityType Activity, SleepDisturbance[]? Sleep = null);

    // El dominio no depende de System.Text.Json: ninguna enum lleva [JsonConverter].
    [Theory]
    [InlineData(typeof(ActivityType))]
    [InlineData(typeof(SleepDisturbance))]
    [InlineData(typeof(SocialFrequency))]
    [InlineData(typeof(SymptomType))]
    public void DomainEnums_DoNotCarryAJsonConverterAttribute(Type enumType)
    {
        Assert.Empty(enumType.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonConverterAttribute), inherit: true));
    }
}
