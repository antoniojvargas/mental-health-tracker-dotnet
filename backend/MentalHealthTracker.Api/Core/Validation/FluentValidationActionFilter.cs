using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using ValidationException = MentalHealthTracker.Domain.Errors.ValidationException;

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

            if (ValidatorResolver.Resolve(serviceProvider, instance.GetType()) is not { } validator)
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
