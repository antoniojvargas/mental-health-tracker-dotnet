using FluentValidation;
using MentalHealthTracker.Api.Core.Hosting;
using MentalHealthTracker.Api.Core.Validation;
using MentalHealthTracker.Api.Modules.DailyLog.Dtos;
using MentalHealthTracker.Api.Modules.DailyLog.Validators;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealthTracker.UnitTests;

public sealed class ValidatorRegistrationTests
{
    private static readonly System.Reflection.Assembly Api = typeof(ValidatorRegistration).Assembly;

    [Fact]
    public void AddValidatorsFromAssembly_RegistersEveryValidatorOfTheApiAsASingletonUnderItsInterface()
    {
        var services = new ServiceCollection().AddValidatorsFromAssembly(Api);

        var registrations = services
            .Where(d => d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition() == typeof(IValidator<>))
            .ToDictionary(d => d.ServiceType, d => d);

        Assert.Equal(3, registrations.Count);
        AssertSingleton<IValidator<CreateDailyLogRequest>, CreateDailyLogValidator>(registrations);
        AssertSingleton<IValidator<ListDailyLogsQuery>, ListDailyLogsQueryValidator>(registrations);
        AssertSingleton<IValidator<TodayQuery>, TodayQueryValidator>(registrations);
    }

    [Fact]
    public void AddApiCore_MakesEveryValidatorResolvable()
    {
        using var provider = new ServiceCollection().AddLogging().AddApiCore().BuildServiceProvider();

        Assert.IsType<CreateDailyLogValidator>(provider.GetRequiredService<IValidator<CreateDailyLogRequest>>());
        Assert.IsType<ListDailyLogsQueryValidator>(provider.GetRequiredService<IValidator<ListDailyLogsQuery>>());
        Assert.IsType<TodayQueryValidator>(provider.GetRequiredService<IValidator<TodayQuery>>());
        Assert.Same(
            provider.GetRequiredService<IValidator<TodayQuery>>(),
            provider.GetRequiredService<IValidator<TodayQuery>>());
    }

    [Fact]
    public void AddValidators_SkipsAbstractGenericAndNonValidatorTypes()
    {
        var services = new ServiceCollection().AddValidators(
            [typeof(AbstractScanValidator), typeof(OpenGenericScanValidator<>), typeof(NotAValidator), typeof(ScanModelValidator)]);

        var registration = Assert.Single(services);
        Assert.Equal(typeof(IValidator<ScanModel>), registration.ServiceType);
        Assert.Equal(typeof(ScanModelValidator), registration.ImplementationType);
    }

    [Fact]
    public void AddValidators_FindsValidatorsThroughAnIntermediateBaseClass()
    {
        var services = new ServiceCollection().AddValidators([typeof(DerivedScanValidator)]);

        var registration = Assert.Single(services);
        Assert.Equal(typeof(IValidator<ScanModel>), registration.ServiceType);
    }

    [Fact]
    public void AddValidators_TwoValidatorsForTheSameModel_FailsNamingBoth()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddValidators([typeof(ScanModelValidator), typeof(DuplicateScanModelValidator)]));

        Assert.Contains(nameof(ScanModelValidator), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(DuplicateScanModelValidator), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ScanModel), exception.Message, StringComparison.Ordinal);
    }

    private static void AssertSingleton<TService, TImplementation>(Dictionary<Type, ServiceDescriptor> registrations)
    {
        var descriptor = registrations[typeof(TService)];

        Assert.Equal(typeof(TImplementation), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }
}

public sealed record ScanModel(string Name);

public sealed class ScanModelValidator : AbstractValidator<ScanModel>
{
}

public sealed class DuplicateScanModelValidator : AbstractValidator<ScanModel>
{
}

public abstract class AbstractScanValidator : AbstractValidator<ScanModel>
{
}

public sealed class DerivedScanValidator : AbstractScanValidator
{
}

public sealed class OpenGenericScanValidator<T> : AbstractValidator<T>
{
}

public sealed class NotAValidator
{
}
