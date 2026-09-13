using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MentalHealthTracker.Infrastructure.Persistence;

internal static class Converters
{
    internal static readonly JsonSerializerOptions SnakeCaseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    internal sealed class EnumSnakeCaseConverter<TEnum> : ValueConverter<TEnum, string>
        where TEnum : struct, Enum
    {
        public EnumSnakeCaseConverter()
            : base(
                value => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString() ?? string.Empty),
                value => Enum.Parse<TEnum>(Enum
                    .GetNames<TEnum>()
                    .First(name => string.Equals(
                        JsonNamingPolicy.SnakeCaseLower.ConvertName(name),
                        value,
                        StringComparison.OrdinalIgnoreCase))))
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
}