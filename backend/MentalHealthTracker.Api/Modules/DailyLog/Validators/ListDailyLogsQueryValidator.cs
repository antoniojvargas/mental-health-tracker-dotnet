using FluentValidation;
using MentalHealthTracker.Api.Modules.DailyLog.Dtos;

namespace MentalHealthTracker.Api.Modules.DailyLog.Validators;

public sealed class ListDailyLogsQueryValidator : AbstractValidator<ListDailyLogsQuery>
{
    private const int MaxRangeDays = 366;
    private const int MaxLimit = 366;

    public ListDailyLogsQueryValidator()
    {
        RuleFor(x => x.Limit)
            .LessThanOrEqualTo(MaxLimit)
            .WithMessage($"limit no puede exceder {MaxLimit}.");

        RuleFor(x => x.Offset)
            .GreaterThanOrEqualTo(0)
            .WithMessage("offset no puede ser negativo.");

        RuleFor(x => x.From)
            .Must((query, from) => !from.HasValue || !query.To.HasValue || from.Value <= query.To.Value)
            .WithMessage("from debe ser menor o igual que to.");

        RuleFor(x => x)
            .Must(query => !query.From.HasValue || !query.To.HasValue ||
                           (query.To.Value - query.From.Value).Days <= MaxRangeDays)
            .WithMessage($"El rango entre from y to no puede exceder {MaxRangeDays} días.");
    }
}