using System.Text.Json;
using System.Text.Json.Serialization;

namespace MentalHealthTracker.Domain.Enums;

public sealed class SnakeCaseEnumJsonConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(SnakeCaseEnumConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class SnakeCaseEnumConverter<T> : JsonConverter<T>
        where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var token = reader.GetString()
                ?? throw new JsonException($"Invalid value for enum {typeof(T).Name}: expected a string.");

            foreach (var name in Enum.GetNames<T>())
            {
                if (string.Equals(
                        JsonNamingPolicy.SnakeCaseLower.ConvertName(name),
                        token,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Enum.Parse<T>(name);
                }
            }

            throw new JsonException($"Invalid value '{token}' for enum {typeof(T).Name}.");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString()));
        }
    }
}