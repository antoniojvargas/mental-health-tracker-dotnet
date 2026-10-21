namespace MentalHealthTracker.IntegrationTests;

// La base de test sale de TEST_DATABASE_URL y, sin ella, de la del compose de test. No toca la
// base: solo la resolución de la cadena.
public sealed class DatabaseUrlTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_WithoutTheVariable_UsesTheComposeDatabase(string? value)
    {
        Assert.Equal(
            "Host=localhost;Port=5433;Database=mht_test;Username=mht_test;Password=mht_test",
            CustomWebApplicationFactory.Resolve(value));
    }

    [Fact]
    public void Resolve_WithTheVariable_UsesItAsIs()
    {
        const string custom = "Host=db.internal;Port=6543;Database=other;Username=u;Password=p";

        Assert.Equal(custom, CustomWebApplicationFactory.Resolve(custom));
    }
}
