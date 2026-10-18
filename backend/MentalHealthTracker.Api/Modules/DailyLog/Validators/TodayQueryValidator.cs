using FluentValidation;
using MentalHealthTracker.Api.Modules.DailyLog.Dtos;

namespace MentalHealthTracker.Api.Modules.DailyLog.Validators;

public sealed class TodayQueryValidator : AbstractValidator<TodayQuery>
{
    public TodayQueryValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.Date)
            .Must(date => date!.Value <= DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddDays(DailyLogDefaults.MaxClientDayAheadOfUtc))
            .When(x => x.Date.HasValue)
            .WithMessage($"date cannot be more than {DailyLogDefaults.MaxClientDayAheadOfUtc} day ahead of the current UTC date.");
    }
}
