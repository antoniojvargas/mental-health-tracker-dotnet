using System.Text.Json;
using System.Text.Json.Serialization;
using MentalHealthTracker.Domain.Common;

namespace MentalHealthTracker.Domain.Enums;

public sealed class SnakeCaseEnumJsonConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(SnakeCaseEnumConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    // Lee y escribe con los mapas precalculados de SnakeCaseEnum<T>: una búsqueda en un
    // diccionario por llamada, sin recorrer los miembros ni convertir nombres cada vez.
    private sealed class SnakeCaseEnumConverter<T> : JsonConverter<T>
        where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var token = reader.GetString()
                ?? throw new JsonException($"Invalid value for enum {typeof(T).Name}: expected a string.");

            return SnakeCaseEnum<T>.TryParse(token, out var value)
                ? value
                : throw new JsonException($"Invalid value '{token}' for enum {typeof(T).Name}.");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(SnakeCaseEnum<T>.ToSnakeCase(value));
        }
    }
}
