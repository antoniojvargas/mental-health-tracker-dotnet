using System.Globalization;
using FluentValidation.Internal;
using FluentValidation.Validators;
using Microsoft.OpenApi;

namespace MentalHealthTracker.Api.Core.OpenApi;

internal static class FluentValidationConstraints
{
    public static void Apply(
        IEnumerable<IRuleComponent> components,
        OpenApiSchema? parent,
        string? propertyName,
        OpenApiSchema property)
    {
        var required = false;

        foreach (var component in components)
        {
            if (component.Validator is not null)
            {
                required |= ApplyConstraint(component.Validator, property);
            }
        }

        if (required && parent is not null && propertyName is not null)
        {
            parent.Required ??= new HashSet<string>();
            parent.Required.Add(propertyName);
        }
    }

    private static bool ApplyConstraint(IPropertyValidator validator, OpenApiSchema property)
    {
        switch (validator)
        {
            case INotNullValidator or INotEmptyValidator:
                return true;

            case IBetweenValidator { From: not null, To: not null } between when validator is IInclusiveBetweenValidator:
                property.Minimum = ToString(between.From);
                property.Maximum = ToString(between.To);
                return false;

            case IBetweenValidator { From: not null, To: not null } between:
                property.ExclusiveMinimum = ToString(between.From);
                property.ExclusiveMaximum = ToString(between.To);
                return false;

            case IComparisonValidator { ValueToCompare: not null } comparison:
                switch (comparison.Comparison)
                {
                    case Comparison.GreaterThanOrEqual:
                        property.Minimum = ToString(comparison.ValueToCompare);
                        break;
                    case Comparison.GreaterThan:
                        property.ExclusiveMinimum = ToString(comparison.ValueToCompare);
                        break;
                    case Comparison.LessThanOrEqual:
                        property.Maximum = ToString(comparison.ValueToCompare);
                        break;
                    case Comparison.LessThan:
                        property.ExclusiveMaximum = ToString(comparison.ValueToCompare);
                        break;
                }
                return false;

            case ILengthValidator length:
                property.MaxLength = length.Max;
                if (length.Min > 0)
                {
                    property.MinLength = length.Min;
                }
                return false;

            default:
                return false;
        }
    }

    private static string ToString(object value) =>
        Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
}