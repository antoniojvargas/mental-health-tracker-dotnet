using System.Text.Json;
using MentalHealthTracker.Domain.Common;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MentalHealthTracker.Infrastructure.Persistence;

internal static class Converters
{
    internal static readonly JsonSerializerOptions SnakeCaseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    // Un valor guardado que ya no existe en la enum no se degrada a un valor por defecto:
    // lanza InvalidOperationException (como hacía el First() sobre una secuencia vacía).
    internal static TEnum ParseStored<TEnum>(string value)
        where TEnum : struct, Enum =>
        SnakeCaseEnum<TEnum>.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Stored value '{value}' is not a valid {typeof(TEnum).Name}.");

    internal sealed class EnumSnakeCaseConverter<TEnum> : ValueConverter<TEnum, string>
        where TEnum : struct, Enum
    {
        public EnumSnakeCaseConverter()
            : base(
                value => SnakeCaseEnum<TEnum>.ToSnakeCase(value),
                value => ParseStored<TEnum>(value))
        {
        }
    }

    internal sealed class JsonListConverter<TItem> : ValueConverter<List<TItem>, string>
    {
        public JsonListConverter()
            : base(
                value => JsonSerializer.Serialize(value, SnakeCaseJsonOptions),
                value => JsonSerializer.Deserialize<List<TItem>>(value, SnakeCaseJsonOptions) ?? new List<TItem>())
        {
        }
    }

    internal sealed class EnumListToStringArrayConverter<TEnum> : ValueConverter<List<TEnum>, List<string>>
        where TEnum : struct, Enum
    {
        public EnumListToStringArrayConverter()
            : base(
                value => value.Select(item => SnakeCaseEnum<TEnum>.ToSnakeCase(item)).ToList(),
                value => value.Select(item => ParseStored<TEnum>(item)).ToList())
        {
        }
    }

    internal static ValueComparer<List<TItem>> ListComparer<TItem>()
    {
        return new ValueComparer<List<TItem>>(
            (left, right) => left!.SequenceEqual(right!),
            values => values!.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
            values => values!.ToList());
    }
}