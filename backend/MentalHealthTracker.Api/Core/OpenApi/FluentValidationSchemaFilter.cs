using System.Reflection;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Internal;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MentalHealthTracker.Api.Core.OpenApi;

public sealed class FluentValidationSchemaFilter(IServiceProvider serviceProvider) : ISchemaFilter
{
    private readonly NullabilityInfoContext _nullabilityInfo = new();

    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        var openApiSchema = schema as OpenApiSchema;
        if (openApiSchema is null)
        {
            return;
        }

        var properties = openApiSchema.Properties;
        if (properties is null || properties.Count == 0)
        {
            return;
        }

        AddNonNullableRequired(context.Type, openApiSchema, properties);

        var validator = ResolveValidator(context.Type);
        if (validator is null || validator is not IEnumerable<IValidationRule> rules)
        {
            return;
        }

        foreach (var rule in rules)
        {
            var propertyName = rule.PropertyName;
            if (string.IsNullOrEmpty(propertyName) || !TryGetPropertySchema(properties, propertyName, out var propertySchema))
            {
                continue;
            }

            var property = propertySchema as OpenApiSchema;
            if (property is null)
            {
                continue;
            }

            FluentValidationConstraints.Apply(rule.Components, openApiSchema, ToOpenApiName(propertyName), property);
        }
    }

    private void AddNonNullableRequired(Type type, OpenApiSchema schema, IDictionary<string, IOpenApiSchema> properties)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (properties.ContainsKey(ToOpenApiName(property.Name)) && IsConceptuallyNonNullable(property))
            {
                schema.Required ??= new HashSet<string>();
                schema.Required.Add(ToOpenApiName(property.Name));
            }
        }
    }

    private bool IsConceptuallyNonNullable(PropertyInfo property)
    {
        if (property.PropertyType.IsValueType)
        {
            return Nullable.GetUnderlyingType(property.PropertyType) is null;
        }

        return _nullabilityInfo.Create(property).ReadState == NullabilityState.NotNull;
    }

    private static bool TryGetPropertySchema(
        IDictionary<string, IOpenApiSchema> properties,
        string propertyName,
        out IOpenApiSchema propertySchema)
    {
        if (properties.TryGetValue(propertyName, out propertySchema!))
        {
            return true;
        }

        return properties.TryGetValue(ToOpenApiName(propertyName), out propertySchema!);
    }

    private static string ToOpenApiName(string propertyName) =>
        JsonNamingPolicy.CamelCase.ConvertName(propertyName);

    private IValidator? ResolveValidator(Type type) =>
        serviceProvider.GetService(typeof(IValidator<>).MakeGenericType(type)) as IValidator;
}