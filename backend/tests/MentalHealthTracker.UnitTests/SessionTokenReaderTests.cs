using MentalHealthTracker.Api.Core.Configuration;
using MentalHealthTracker.Api.Modules.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.UnitTests;

public sealed class SessionTokenReaderTests
{
    private static readonly Guid UserId = Guid.Parse("3f2c1a9e-7b4d-4e8a-9c55-0d6e2b1f4a73");

    private readonly CountingJwtService _jwt = new(
        Options.Create(new JwtOptions { Secret = "unit-test-jwt-secret-key-value" }));

    private SessionTokenReader Reader => new(_jwt);

    private static DefaultHttpContext ContextWithCookie(string? token)
    {
        var context = new DefaultHttpContext();
        if (token is not null)
        {
            context.Request.Headers.Cookie = $"{SessionCookie.AccessTokenName}={token}";
        }

        return context;
    }

    [Fact]
    public void Read_ValidToken_VerifiesOnceAcrossRepeatedReads()
    {
        var reader = Reader;
        var context = ContextWithCookie(_jwt.Sign(UserId, "ana@example.com"));

        var first = reader.Read(context);
        var second = reader.Read(context);

        Assert.Equal((UserId, "ana@example.com"), first);
        Assert.Equal(first, second);
        Assert.Equal(1, _jwt.VerifyCalls);
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData(null)]
    public void Read_InvalidOrMissingToken_CachesTheNegativeResultToo(string? token)
    {
        var reader = Reader;
        var context = ContextWithCookie(token);

        Assert.Null(reader.Read(context));
        Assert.Null(reader.Read(context));
        Assert.Equal(1, _jwt.VerifyCalls);
    }

    [Fact]
    public void Read_DifferentRequests_DoNotShareTheCache()
    {
        var reader = Reader;
        var token = _jwt.Sign(UserId, "ana@example.com");

        reader.Read(ContextWithCookie(token));
        reader.Read(ContextWithCookie(token));

        Assert.Equal(2, _jwt.VerifyCalls);
    }

    private sealed class CountingJwtService(IOptions<JwtOptions> options) : JwtService(options, TimeProvider.System)
    {
        public int VerifyCalls { get; private set; }

        public override (Guid UserId, string Email)? Verify(string? token)
        {
            VerifyCalls++;
            return base.Verify(token);
        }
    }
}
