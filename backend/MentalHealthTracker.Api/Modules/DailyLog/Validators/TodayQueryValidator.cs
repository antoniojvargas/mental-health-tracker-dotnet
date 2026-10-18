using FluentValidation;
using MentalHealthTracker.Api.Modules.DailyLog.Dtos;

namespace MentalHealthTracker.Api.Modules.DailyLog.Validators;

public sealed class TodayQueryValidator : AbstractValidator<TodayQuery>
{
    // UTC+14 es la zona más adelantada: allí ya es "mañana" respecto al día UTC.
    private const int MaxDaysAheadOfUtc = 1;

    public TodayQueryValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.Date)
            .Must(date => date!.Value <= DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddDays(MaxDaysAheadOfUtc))
            .When(x => x.Date.HasValue)
            .WithMessage($"date cannot be more than {MaxDaysAheadOfUtc} day ahead of the current UTC date.");
    }
}
