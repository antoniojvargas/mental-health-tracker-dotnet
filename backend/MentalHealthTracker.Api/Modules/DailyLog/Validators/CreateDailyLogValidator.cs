using FluentValidation;
using MentalHealthTracker.Api.Modules.DailyLog.Dtos;

namespace MentalHealthTracker.Api.Modules.DailyLog.Validators;

public sealed class CreateDailyLogValidator : AbstractValidator<CreateDailyLogRequest>
{
    public CreateDailyLogValidator()
    {
        RuleFor(x => x.MoodRating).InclusiveBetween(1, 5);

        RuleFor(x => x.AnxietyLevel).InclusiveBetween(1, 10);

        RuleFor(x => x.StressLevel).InclusiveBetween(1, 10);

        RuleFor(x => x.SleepHours).InclusiveBetween(0, 24);

        RuleFor(x => x.SleepQuality).InclusiveBetween(1, 5);

        RuleFor(x => x.ActivityMinutes)
            .InclusiveBetween(0, 600)
            .When(x => x.ActivityMinutes.HasValue);

        RuleFor(x => x.Notes).MaximumLength(1000);

        RuleForEach(x => x.Symptoms).ChildRules(symptoms =>
        {
            symptoms.RuleFor(symptom => symptom.Severity).InclusiveBetween(1, 5);
        });
    }
}