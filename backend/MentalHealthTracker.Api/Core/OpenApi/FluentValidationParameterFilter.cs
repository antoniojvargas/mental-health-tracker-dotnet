using FluentValidation;
using FluentValidation.Internal;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MentalHealthTracker.Api.Core.OpenApi;

public sealed class FluentValidationParameterFilter(IServiceProvider serviceProvider) : IParameterFilter
{
    public void Apply(IOpenApiParameter parameter, ParameterFilterContext context)
    {
        var propertyName = context.PropertyInfo?.Name;
        var declaringType = context.PropertyInfo?.DeclaringType;
        if (propertyName is null || declaringType is null)
        {
            return;
        }

        var parameterSchema = parameter.Schema as OpenApiSchema;
        if (parameterSchema is null)
        {
            return;
        }

        var validator = ResolveValidator(declaringType);
        if (validator is null)
        {
            return;
        }

        if (validator is not IEnumerable<IValidationRule> rules)
        {
            return;
        }

        foreach (var rule in rules)
        {
            if (rule.PropertyName == propertyName)
            {
                FluentValidationConstraints.Apply(rule.Components, parent: null, propertyName: null, parameterSchema);
            }
        }
    }

    private IValidator? ResolveValidator(Type type) =>
        serviceProvider.GetService(typeof(IValidator<>).MakeGenericType(type)) as IValidator;
}