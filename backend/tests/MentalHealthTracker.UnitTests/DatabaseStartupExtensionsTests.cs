using MentalHealthTracker.Api.Core.Configuration;
using MentalHealthTracker.Api.Core.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.UnitTests;

// Cuándo migra el arranque: por defecto sí (una sola instancia), Database:MigrateOnStartup=false
// lo apaga para el despliegue con réplicas, y --migrate / --seed migran siempre porque ese es
// su trabajo. --migrate además termina sin arrancar el servidor.
public sealed class DatabaseStartupExtensionsTests
{
    [Fact]
    public void MigrateOnStartup_DefaultsToTrue_SoTheCurrentBehaviourIsKept()
    {
        Assert.True(new DatabaseOptions().MigrateOnStartup);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void MigrateOnStartup_IsBoundFromConfiguration(string? value, bool expected)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] = "Host=localhost;Database=x;Username=x;Password=x",
        };
        if (value is not null)
        {
            settings["Database:MigrateOnStartup"] = value;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        using var provider = new ServiceCollection()
            .AddValidatedOptions<DatabaseOptions>(configuration, DatabaseOptions.SectionName)
            .BuildServiceProvider();

        Assert.Equal(expected, provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.MigrateOnStartup);
    }

    [Theory]
    [InlineData(true, new string[0], true)]
    [InlineData(false, new string[0], false)]
    [InlineData(false, new[] { "--migrate" }, true)]
    [InlineData(false, new[] { "--seed" }, true)]
    [InlineData(false, new[] { "--other" }, false)]
    [InlineData(true, new[] { "--migrate" }, true)]
    public void ShouldMigrate_FollowsTheOptionAndTheFlags(bool migrateOnStartup, string[] args, bool expected)
    {
        Assert.Equal(expected, DatabaseStartupExtensions.ShouldMigrate(migrateOnStartup, args));
    }

    [Theory]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "--migrate" }, true)]
    [InlineData(new[] { "--seed" }, false)]
    [InlineData(new[] { "--seed", "--migrate" }, true)]
    public void IsMigrateOnly_IsTrueOnlyWithTheMigrateFlag(string[] args, bool expected)
    {
        Assert.Equal(expected, DatabaseStartupExtensions.IsMigrateOnly(args));
    }
}
