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

    [Fact]
    public void TryGetUserId_WithRepeatedClaims_ReadsTheFirstOne()
    {
        var first = Guid.NewGuid();

        var found = PrincipalWith(
            new Claim(ClaimTypes.NameIdentifier, first.ToString()),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())).TryGetUserId(out var userId);

        Assert.True(found);
        Assert.Equal(first, userId);
    }

    [Fact]
    public void TryGetUserId_OnlyReadsTheNameIdentifierClaimType()
    {
        var found = PrincipalWith(new Claim("sub", Guid.NewGuid().ToString())).TryGetUserId(out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGetUserId_ReadsTheClaimFromAnyOfThePrincipalsIdentities()
    {
        var id = Guid.NewGuid();
        var principal = new ClaimsPrincipal();
        principal.AddIdentity(new ClaimsIdentity([new Claim(ClaimTypes.Email, "ana@example.com")], "a"));
        principal.AddIdentity(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "b"));

        Assert.True(principal.TryGetUserId(out var userId));
        Assert.Equal(id, userId);
    }

    [Fact]
    public void TryGetUserId_WithAnEmptyPrincipal_ReturnsFalse()
    {
        Assert.False(new ClaimsPrincipal().TryGetUserId(out var userId));
        Assert.Equal(Guid.Empty, userId);
    }

    [Theory]
    [InlineData("N")]
    [InlineData("D")]
    [InlineData("B")]
    public void TryGetUserId_AcceptsTheStandardGuidFormats(string format)
    {
        var id = Guid.NewGuid();

        Assert.True(PrincipalWith(new Claim(ClaimTypes.NameIdentifier, id.ToString(format))).TryGetUserId(out var userId));
        Assert.Equal(id, userId);
    }
}
