using MentalHealthTracker.Domain.Enums;
using MentalHealthTracker.Domain.ValueObjects;
using MentalHealthTracker.Infrastructure.Persistence;

namespace MentalHealthTracker.UnitTests;

// Pruebas de caracterización de los convertidores de EF Core que escriben y leen las
// columnas del registro diario (internos de Infrastructure, visibles por InternalsVisibleTo).
// Los strings de la base de datos están escritos a mano: son el formato que ya hay
// guardado, y cambiarlo sin migrar datos rompería la lectura de los registros existentes.
public sealed class PersistenceConvertersTests
{
    // --- EnumSnakeCaseConverter ---

    [Theory]
    [InlineData(ActivityType.None, "none")]
    [InlineData(ActivityType.Walking, "walking")]
    [InlineData(ActivityType.Other, "other")]
    public void EnumSnakeCase_ActivityType_RoundTrips(ActivityType value, string stored)
    {
        var converter = new Converters.EnumSnakeCaseConverter<ActivityType>();

        Assert.Equal(stored, converter.ConvertToProvider(value));
        Assert.Equal(value, converter.ConvertFromProvider(stored));
    }

    [Theory]
    [InlineData(SocialFrequency.None, "none")]
    [InlineData(SocialFrequency.Rare, "rare")]
    [InlineData(SocialFrequency.Occasional, "occasional")]
    [InlineData(SocialFrequency.Frequent, "frequent")]
    [InlineData(SocialFrequency.Daily, "daily")]
    public void EnumSnakeCase_SocialFrequency_RoundTrips(SocialFrequency value, string stored)
    {
        var converter = new Converters.EnumSnakeCaseConverter<SocialFrequency>();

        Assert.Equal(stored, converter.ConvertToProvider(value));
        Assert.Equal(value, converter.ConvertFromProvider(stored));
    }

    [Fact]
    public void EnumSnakeCase_ReadsWithoutCaseSensitivity()
    {
        var converter = new Converters.EnumSnakeCaseConverter<SleepDisturbance>();

        Assert.Equal(SleepDisturbance.FrequentWaking, converter.ConvertFromProvider("FREQUENT_WAKING"));
    }

    [Fact]
    public void EnumSnakeCase_UnknownStoredValue_ThrowsInvalidOperation()
    {
        // First() sobre una secuencia vacía: un valor guardado que ya no existe en la enum
        // no se degrada a un valor por defecto, revienta.
        var converter = new Converters.EnumSnakeCaseConverter<ActivityType>();

        Assert.Throws<InvalidOperationException>(() => converter.ConvertFromProvider("swimming"));
    }

    // --- EnumListToStringArrayConverter ---

    [Fact]
    public void EnumListToStringArray_WritesSnakeCaseStringsInOrder()
    {
        var converter = new Converters.EnumListToStringArrayConverter<SleepDisturbance>();

        var stored = (List<string>)converter.ConvertToProvider(
            new List<SleepDisturbance> { SleepDisturbance.FrequentWaking, SleepDisturbance.Insomnia, SleepDisturbance.EarlyWaking })!;

        Assert.Equal(["frequent_waking", "insomnia", "early_waking"], stored);
    }

    [Fact]
    public void EnumListToStringArray_ReadsBackTheSameListInOrder()
    {
        var converter = new Converters.EnumListToStringArrayConverter<SleepDisturbance>();

        var read = (List<SleepDisturbance>)converter.ConvertFromProvider(
            new List<string> { "early_waking", "nightmares", "none" })!;

        Assert.Equal([SleepDisturbance.EarlyWaking, SleepDisturbance.Nightmares, SleepDisturbance.None], read);
    }

    [Fact]
    public void EnumListToStringArray_EmptyListRoundTripsToEmpty()
    {
        var converter = new Converters.EnumListToStringArrayConverter<SleepDisturbance>();

        Assert.Empty((List<string>)converter.ConvertToProvider(new List<SleepDisturbance>())!);
        Assert.Empty((List<SleepDisturbance>)converter.ConvertFromProvider(new List<string>())!);
    }

    [Fact]
    public void EnumListToStringArray_UnknownStoredValue_ThrowsInvalidOperation()
    {
        var converter = new Converters.EnumListToStringArrayConverter<SleepDisturbance>();

        Assert.Throws<InvalidOperationException>(
            () => converter.ConvertFromProvider(new List<string> { "sleepwalking" }));
    }

    // --- JsonListConverter<Symptom> ---

    [Fact]
    public void JsonList_Symptom_WritesTheExactJson()
    {
        var converter = new Converters.JsonListConverter<Symptom>();

        var json = converter.ConvertToProvider(new List<Symptom> { new(SymptomType.Fatigue, 3) });

        Assert.Equal("""[{"type":"fatigue","severity":3}]""", json);
    }

    [Fact]
    public void JsonList_Symptom_WritesSeveralItemsWithSnakeCaseTypes()
    {
        var converter = new Converters.JsonListConverter<Symptom>();

        var json = converter.ConvertToProvider(
            new List<Symptom> { new(SymptomType.AppetiteChange, 1), new(SymptomType.LowMood, 5) });

        Assert.Equal(
            """[{"type":"appetite_change","severity":1},{"type":"low_mood","severity":5}]""",
            json);
    }

    [Fact]
    public void JsonList_Symptom_ReadsTheJsonBack()
    {
        var converter = new Converters.JsonListConverter<Symptom>();

        var read = (List<Symptom>)converter.ConvertFromProvider("""[{"type":"fatigue","severity":3}]""")!;

        Assert.Equal([new Symptom(SymptomType.Fatigue, 3)], read);
    }

    [Fact]
    public void JsonList_Symptom_EmptyListIsAnEmptyJsonArray()
    {
        var converter = new Converters.JsonListConverter<Symptom>();

        Assert.Equal("[]", converter.ConvertToProvider(new List<Symptom>()));
        Assert.Empty((List<Symptom>)converter.ConvertFromProvider("[]")!);
    }

    [Fact]
    public void JsonList_Symptom_JsonNullReadsAsEmptyList()
    {
        var converter = new Converters.JsonListConverter<Symptom>();

        Assert.Empty((List<Symptom>)converter.ConvertFromProvider("null")!);
    }

    // --- ListComparer ---

    [Fact]
    public void ListComparer_EqualsComparesElementsInOrder()
    {
        var comparer = Converters.ListComparer<SleepDisturbance>();

        Assert.True(comparer.Equals(
            [SleepDisturbance.Insomnia, SleepDisturbance.Nightmares],
            [SleepDisturbance.Insomnia, SleepDisturbance.Nightmares]));
        Assert.False(comparer.Equals(
            [SleepDisturbance.Insomnia, SleepDisturbance.Nightmares],
            [SleepDisturbance.Nightmares, SleepDisturbance.Insomnia]));
        Assert.False(comparer.Equals([SleepDisturbance.Insomnia], []));
    }

    [Fact]
    public void ListComparer_ComparesRecordsByValue()
    {
        var comparer = Converters.ListComparer<Symptom>();

        Assert.True(comparer.Equals([new Symptom(SymptomType.Panic, 2)], [new Symptom(SymptomType.Panic, 2)]));
        Assert.False(comparer.Equals([new Symptom(SymptomType.Panic, 2)], [new Symptom(SymptomType.Panic, 3)]));
    }

    [Fact]
    public void ListComparer_EqualListsHaveEqualHashes()
    {
        var comparer = Converters.ListComparer<Symptom>();
        List<Symptom> left = [new(SymptomType.Panic, 2), new(SymptomType.Fatigue, 4)];
        List<Symptom> right = [new(SymptomType.Panic, 2), new(SymptomType.Fatigue, 4)];

        Assert.Equal(comparer.GetHashCode(left), comparer.GetHashCode(right));
    }

    [Fact]
    public void ListComparer_SnapshotIsAnIndependentCopy()
    {
        var comparer = Converters.ListComparer<SleepDisturbance>();
        List<SleepDisturbance> original = [SleepDisturbance.Insomnia];

        var snapshot = comparer.Snapshot(original)!;
        original.Add(SleepDisturbance.Nightmares);

        Assert.NotSame(original, snapshot);
        Assert.Equal([SleepDisturbance.Insomnia], snapshot);
    }
}
