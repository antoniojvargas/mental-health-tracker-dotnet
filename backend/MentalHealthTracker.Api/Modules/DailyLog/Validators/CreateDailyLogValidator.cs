using System.Globalization;
using System.Text.RegularExpressions;
using FluentValidation;
using MentalHealthTracker.Api.Modules.DailyLog.Dtos;

namespace MentalHealthTracker.Api.Modules.DailyLog.Validators;

public sealed class CreateDailyLogValidator : AbstractValidator<CreateDailyLogRequest>
{
    private static readonly Regex DateOnlyFormatRegex = new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);

    public CreateDailyLogValidator()
    {
        RuleFor(x => x.LogDate)
            .Must(x => DateOnlyFormatRegex.IsMatch(x.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))
            .WithMessage("logDate debe tener formato YYYY-MM-DD.");

        RuleFor(x => x.LogDate)
            .Must(x => x <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("logDate no puede estar en el futuro.");

        RuleFor(x => x.MoodRating).InclusiveBetween((short)1, (short)5);

        RuleFor(x => x.AnxietyLevel).InclusiveBetween((short)1, (short)10);

        RuleFor(x => x.StressLevel).InclusiveBetween((short)1, (short)10);

        RuleFor(x => x.SleepHours).InclusiveBetween(0m, 24m);

        RuleFor(x => x.SleepQuality).InclusiveBetween((short)1, (short)5);

        RuleFor(x => x.ActivityMinutes)
            .InclusiveBetween((short)0, (short)600)
            .When(x => x.ActivityMinutes.HasValue);

        RuleFor(x => x.Notes).MaximumLength(1000);

        RuleForEach(x => x.Symptoms).ChildRules(symptoms =>
        {
            symptoms.RuleFor(symptom => symptom.Severity).InclusiveBetween(1, 5);
        });
    }
}