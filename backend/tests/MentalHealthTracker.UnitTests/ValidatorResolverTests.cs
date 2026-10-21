using FluentValidation;
using MentalHealthTracker.Api.Core.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealthTracker.UnitTests;

public sealed class ValidatorResolverTests
{
    [Fact]
    public void ValidatorTypeFor_IsTheClosedIValidatorOfTheModel()
    {
        Assert.Equal(typeof(IValidator<ResolverModelA>), ValidatorResolver.ValidatorTypeFor(typeof(ResolverModelA)));
        Assert.Equal(typeof(IValidator<ResolverModelB>), ValidatorResolver.ValidatorTypeFor(typeof(ResolverModelB)));
    }

    [Fact]
    public void ValidatorTypeFor_CachesOneEntryPerModelType()
    {
        var before = ValidatorResolver.CachedTypeCount;

        for (var i = 0; i < 50; i++)
        {
            ValidatorResolver.ValidatorTypeFor(typeof(ResolverCachedModel));
        }

        Assert.Equal(before + 1, ValidatorResolver.CachedTypeCount);
    }

    [Fact]
    public void ValidatorTypeFor_IsSafeUnderConcurrentFirstUse()
    {
        var results = new Type[64];

        Parallel.For(0, results.Length, i => results[i] = ValidatorResolver.ValidatorTypeFor(typeof(ResolverConcurrentModel)));

        Assert.All(results, type => Assert.Equal(typeof(IValidator<ResolverConcurrentModel>), type));
    }

    [Fact]
    public void Resolve_ReturnsTheRegisteredValidator()
    {
        using var provider = new ServiceCollection()
            .AddSingleton<IValidator<ResolverModelA>, ResolverModelAValidator>()
            .BuildServiceProvider();

        var validator = ValidatorResolver.Resolve(provider, typeof(ResolverModelA));

        Assert.IsType<ResolverModelAValidator>(validator);
    }

    [Fact]
    public void Resolve_WithoutARegisteredValidator_ReturnsNull()
    {
        using var provider = new ServiceCollection()
            .AddSingleton<IValidator<ResolverModelA>, ResolverModelAValidator>()
            .BuildServiceProvider();

        Assert.Null(ValidatorResolver.Resolve(provider, typeof(ResolverModelB)));
    }

    [Fact]
    public void Resolve_HonoursEachProvidersRegistrationsWhileSharingTheTypeCache()
    {
        using var withValidator = new ServiceCollection()
            .AddSingleton<IValidator<ResolverModelB>, ResolverModelBValidator>()
            .BuildServiceProvider();
        using var without = new ServiceCollection().BuildServiceProvider();

        Assert.NotNull(ValidatorResolver.Resolve(withValidator, typeof(ResolverModelB)));
        Assert.Null(ValidatorResolver.Resolve(without, typeof(ResolverModelB)));
    }

    [Fact]
    public void Resolve_ReturnsTheSameInstanceForASingleton()
    {
        using var provider = new ServiceCollection()
            .AddSingleton<IValidator<ResolverModelA>, ResolverModelAValidator>()
            .BuildServiceProvider();

        Assert.Same(
            ValidatorResolver.Resolve(provider, typeof(ResolverModelA)),
            ValidatorResolver.Resolve(provider, typeof(ResolverModelA)));
    }

    [Fact]
    public void Resolve_ReturnsANewInstanceForATransient()
    {
        using var provider = new ServiceCollection()
            .AddTransient<IValidator<ResolverModelA>, ResolverModelAValidator>()
            .BuildServiceProvider();

        Assert.NotSame(
            ValidatorResolver.Resolve(provider, typeof(ResolverModelA)),
            ValidatorResolver.Resolve(provider, typeof(ResolverModelA)));
    }

    [Fact]
    public void Resolve_ASingletonValidatorWorksThroughTheResolvedInterface()
    {
        using var provider = new ServiceCollection()
            .AddSingleton<IValidator<ResolverModelA>, ResolverModelAValidator>()
            .BuildServiceProvider();

        var validator = ValidatorResolver.Resolve(provider, typeof(ResolverModelA));

        Assert.True(validator!.Validate(new ValidationContext<ResolverModelA>(new ResolverModelA("x"))).IsValid);
    }

    [Fact]
    public void ValidatorTypeFor_DistinctModelsGetDistinctTypes()
    {
        Assert.NotEqual(
            ValidatorResolver.ValidatorTypeFor(typeof(ResolverModelA)),
            ValidatorResolver.ValidatorTypeFor(typeof(ResolverModelB)));
    }
}

public sealed record ResolverModelA(string Name);

public sealed record ResolverModelB(string Name);

public sealed record ResolverCachedModel(string Name);

public sealed record ResolverConcurrentModel(string Name);

public sealed class ResolverModelAValidator : AbstractValidator<ResolverModelA>
{
}

public sealed class ResolverModelBValidator : AbstractValidator<ResolverModelB>
{
}
