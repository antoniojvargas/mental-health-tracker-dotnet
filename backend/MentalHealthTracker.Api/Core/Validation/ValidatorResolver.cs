using System.Collections.Concurrent;
using FluentValidation;

namespace MentalHealthTracker.Api.Core.Validation;

// Resuelve el IValidator<T> registrado para un tipo conocido solo en tiempo de ejecución. El
// tipo cerrado IValidator<T> se construye una vez por tipo (MakeGenericType no es gratis y se
// pedía en cada petición); es estático porque el filtro de acciones se instancia por petición y
// los de OpenAPI por documento, y los tipos cerrados son globales de todos modos.
public static class ValidatorResolver
{
    private static readonly ConcurrentDictionary<Type, Type> ValidatorTypes = new();

    // Número de tipos cerrados en la caché; solo para las pruebas.
    internal static int CachedTypeCount => ValidatorTypes.Count;

    public static Type ValidatorTypeFor(Type modelType) =>
        ValidatorTypes.GetOrAdd(modelType, static type => typeof(IValidator<>).MakeGenericType(type));

    // El validador registrado para el tipo, o null si no hay ninguno.
    public static IValidator? Resolve(IServiceProvider serviceProvider, Type modelType) =>
        serviceProvider.GetService(ValidatorTypeFor(modelType)) as IValidator;
}
