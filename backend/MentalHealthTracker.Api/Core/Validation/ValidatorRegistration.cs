using System.Reflection;
using FluentValidation;

namespace MentalHealthTracker.Api.Core.Validation;

public static class ValidatorRegistration
{
    // Registra como singleton, bajo IValidator<T>, cada clase concreta del ensamblado que
    // hereda de AbstractValidator<T>: añadir un validador nuevo no exige tocar ningún módulo.
    // No se registran las abstractas ni las genéricas abiertas, y si dos validadores
    // comparten modelo falla el arranque, porque el filtro resolvería uno al azar.
    public static IServiceCollection AddValidatorsFromAssembly(this IServiceCollection services, Assembly assembly) =>
        services.AddValidators(assembly.GetTypes());

    internal static IServiceCollection AddValidators(this IServiceCollection services, IEnumerable<Type> candidates)
    {
        var registered = new Dictionary<Type, Type>();

        foreach (var implementation in candidates)
        {
            if (implementation.IsAbstract || implementation.IsGenericTypeDefinition)
            {
                continue;
            }

            var modelType = ModelTypeOf(implementation);
            if (modelType is null)
            {
                continue;
            }

            if (registered.TryGetValue(modelType, out var existing))
            {
                throw new InvalidOperationException(
                    $"Both {existing.FullName} and {implementation.FullName} validate {modelType.FullName}; " +
                    "there must be a single validator per model.");
            }

            registered.Add(modelType, implementation);
            services.AddSingleton(ValidatorResolver.ValidatorTypeFor(modelType), implementation);
        }

        return services;
    }

    // El T de AbstractValidator<T> del que desciende el tipo, o null si no es un validador.
    private static Type? ModelTypeOf(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
            {
                return current.GetGenericArguments()[0];
            }
        }

        return null;
    }
}
