using System.Security.Claims;
using MentalHealthTracker.Api.Modules.Auth;

namespace MentalHealthTracker.UnitTests;

public sealed class ClaimsPrincipalExtensionsTests
{
    private static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));

    [Fact]
    public void TryGetUserId_WithAGuidNameIdentifier_ReturnsTrueAndTheId()
    {
        var id = Guid.NewGuid();

        var found = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, id.ToString())).TryGetUserId(out var userId);

        Assert.True(found);
        Assert.Equal(id, userId);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGetUserId_WithAnInvalidClaimValue_ReturnsFalse(string value)
    {
        var found = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, value)).TryGetUserId(out var userId);

        Assert.False(found);
        Assert.Equal(Guid.Empty, userId);
    }

    [Fact]
    public void TryGetUserId_WithoutTheNameIdentifierClaim_ReturnsFalse()
    {
        var found = PrincipalWith(new Claim(ClaimTypes.Email, "ana@example.com")).TryGetUserId(out var userId);

        Assert.False(found);
        Assert.Equal(Guid.Empty, userId);
    }

    [Fact]
    public void TryGetUserId_WithANullPrincipal_ReturnsFalse()
    {
        ClaimsPrincipal? principal = null;

        Assert.False(principal.TryGetUserId(out var userId));
        Assert.Equal(Guid.Empty, userId);
    }
}
