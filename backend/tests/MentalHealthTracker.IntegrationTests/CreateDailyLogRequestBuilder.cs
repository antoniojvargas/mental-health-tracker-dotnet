using MentalHealthTracker.Api.Modules.DailyLog.Dtos;
using MentalHealthTracker.Domain.Enums;
using MentalHealthTracker.Domain.ValueObjects;

namespace MentalHealthTracker.IntegrationTests;

// Constructor de CreateDailyLogRequest con valores válidos por defecto, para que una
// prueba solo tenga que declarar lo que realmente quiere variar. Los por defecto se
// eligen deliberadamente dentro de los rangos de CreateDailyLogValidator (mood 1-5,
// anxiety/stress 1-10, sleep hours 0-24, sleep quality 1-5, activity minutes 0-600,
// severity de síntoma 1-5) y con LogDate en el día de hoy, que el validador exige que no
// sea futuro. Un With* que se pase de rango produce una petición inválida a propósito:
// es lo que quieren las pruebas de validación.
public sealed class CreateDailyLogRequestBuilder
{
    private DateOnly _logDate = DateOnly.FromDateTime(DateTime.UtcNow);

    private short _moodRating = 3;

    private short _anxietyLevel = 5;

    private short _stressLevel = 5;

    private decimal _sleepHours = 7.5m;

    private short _sleepQuality = 4;

    private List<SleepDisturbance> _sleepDisturbances = [SleepDisturbance.None];

    private ActivityType? _activityType = ActivityType.Walking;

    private short? _activityMinutes = 30;

    private SocialFrequency _socialFrequency = SocialFrequency.Occasional;

    private List<Symptom> _symptoms = [new Symptom(SymptomType.Fatigue, 3)];

    private string? _notes;

    public CreateDailyLogRequestBuilder WithLogDate(DateOnly logDate)
    {
        _logDate = logDate;
        return this;
    }

    public CreateDailyLogRequestBuilder WithMoodRating(short moodRating)
    {
        _moodRating = moodRating;
        return this;
    }

    public CreateDailyLogRequestBuilder WithAnxietyLevel(short anxietyLevel)
    {
        _anxietyLevel = anxietyLevel;
        return this;
    }

    public CreateDailyLogRequestBuilder WithStressLevel(short stressLevel)
    {
        _stressLevel = stressLevel;
        return this;
    }

    public CreateDailyLogRequestBuilder WithSleepHours(decimal sleepHours)
    {
        _sleepHours = sleepHours;
        return this;
    }

    public CreateDailyLogRequestBuilder WithSleepQuality(short sleepQuality)
    {
        _sleepQuality = sleepQuality;
        return this;
    }

    public CreateDailyLogRequestBuilder WithSleepDisturbances(List<SleepDisturbance> sleepDisturbances)
    {
        _sleepDisturbances = sleepDisturbances;
        return this;
    }

    public CreateDailyLogRequestBuilder WithActivityType(ActivityType? activityType)
    {
        _activityType = activityType;
        return this;
    }

    public CreateDailyLogRequestBuilder WithActivityMinutes(short? activityMinutes)
    {
        _activityMinutes = activityMinutes;
        return this;
    }

    public CreateDailyLogRequestBuilder WithSocialFrequency(SocialFrequency socialFrequency)
    {
        _socialFrequency = socialFrequency;
        return this;
    }

    public CreateDailyLogRequestBuilder WithSymptoms(List<Symptom> symptoms)
    {
        _symptoms = symptoms;
        return this;
    }

    public CreateDailyLogRequestBuilder WithNotes(string? notes)
    {
        _notes = notes;
        return this;
    }

    public CreateDailyLogRequest Build() => new(
        LogDate: _logDate,
        MoodRating: _moodRating,
        AnxietyLevel: _anxietyLevel,
        StressLevel: _stressLevel,
        SleepHours: _sleepHours,
        SleepQuality: _sleepQuality,
        SleepDisturbances: _sleepDisturbances,
        ActivityType: _activityType,
        ActivityMinutes: _activityMinutes,
        SocialFrequency: _socialFrequency,
        Symptoms: _symptoms,
        Notes: _notes);
}
