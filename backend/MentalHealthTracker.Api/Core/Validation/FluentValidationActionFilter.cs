using FluentValidation;
using MentalHealthTracker.Domain.Errors;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MentalHealthTracker.Api.Core.Validation;

public sealed class FluentValidationActionFilter(IServiceProvider serviceProvider) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var instance in context.ActionArguments.Values)
        {
            if (instance is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(instance.GetType());
            if (serviceProvider.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationResult = await validator.ValidateAsync(
                new ValidationContext<object>(instance),
                context.HttpContext.RequestAborted);

            if (!validationResult.IsValid)
            {
                var details = validationResult.Errors
                    .GroupBy(error => error.PropertyName)
                    .ToDictionary(group => group.Key, group => group.First().ErrorMessage);

                throw new ValidationException("Validation failed.", details);
            }
        }

        await next();
    }
}
