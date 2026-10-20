using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace MentalHealthTracker.Domain.Common;

// Correspondencia entre los miembros de una enum y su nombre en snake_case (el del cable y
// el de la base de datos), calculada una sola vez por enum en dos mapas inmutables, uno por
// sentido. Reemplaza el recorrido de Enum.GetNames con ConvertName en cada lectura. El nombre
// es el de JsonNamingPolicy.SnakeCaseLower aplicado al miembro; la lectura ignora mayúsculas,
// como hacen hoy el convertidor JSON y el de EF. Las enums no deben repetir valores.
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "El nombre describe lo que es (la vista snake_case de una enum) y es el que usan sus consumidores.")]
[SuppressMessage(
    "Design",
    "CA1000:Do not declare static members on generic types",
    Justification = "El tipo genérico estático ES la caché: una pareja de mapas por enum, construida una sola vez, sin instancias ni diccionario por tipo.")]
public static class SnakeCaseEnum<TEnum>
    where TEnum : struct, Enum
{
    private static readonly FrozenDictionary<TEnum, string> NameByValue =
        Enum.GetValues<TEnum>().ToFrozenDictionary(
            value => value,
            value => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString()));

    private static readonly FrozenDictionary<string, TEnum> ValueByName =
        Enum.GetValues<TEnum>().ToFrozenDictionary(
            value => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString()),
            value => value,
            StringComparer.OrdinalIgnoreCase);

    // Nombre snake_case del miembro; lanza si el valor no es un miembro definido de la enum.
    public static string ToSnakeCase(TEnum value) =>
        NameByValue.TryGetValue(value, out var name)
            ? name
            : throw new ArgumentOutOfRangeException(nameof(value), value, $"Not a defined {typeof(TEnum).Name} member.");

    public static bool TryParse(string? snakeCase, out TEnum value)
    {
        value = default;

        return snakeCase is not null && ValueByName.TryGetValue(snakeCase, out value);
    }

    public static TEnum Parse(string? snakeCase) =>
        TryParse(snakeCase, out var value)
            ? value
            : throw new FormatException($"Invalid value '{snakeCase}' for enum {typeof(TEnum).Name}.");
}
