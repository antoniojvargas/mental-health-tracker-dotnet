using FluentValidation;
using MentalHealthTracker.Api.Modules.DailyLog.Dtos;

namespace MentalHealthTracker.Api.Modules.DailyLog.Validators;

public sealed class ListDailyLogsQueryValidator : AbstractValidator<ListDailyLogsQuery>
{
    public ListDailyLogsQueryValidator()
    {
        RuleFor(x => x.Limit)
            .LessThanOrEqualTo(DailyLogDefaults.MaxLimit)
            .WithMessage($"limit cannot exceed {DailyLogDefaults.MaxLimit}.");

        RuleFor(x => x.Offset)
            .GreaterThanOrEqualTo(0)
            .WithMessage("offset cannot be negative.");

        RuleFor(x => x.From)
            .Must((query, from) => !from.HasValue || !query.To.HasValue || from.Value <= query.To.Value)
            .WithMessage("from must be less than or equal to to.");

        RuleFor(x => x)
            .Must(query => !query.From.HasValue || !query.To.HasValue ||
                           (query.To.Value.DayNumber - query.From.Value.DayNumber) <= DailyLogDefaults.MaxRangeDays)
            .WithMessage($"The range between from and to cannot exceed {DailyLogDefaults.MaxRangeDays} days.");
    }
}