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

    [Fact]
    public void Read_IgnoresAValidTokenInAnotherCookie()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"other_cookie={_jwt.Sign(UserId, "ana@example.com")}";

        Assert.Null(Reader.Read(context));
    }

    [Fact]
    public void Read_ATokenWithATamperedSignature_ReturnsNull()
    {
        var token = _jwt.Sign(UserId, "ana@example.com");
        var segments = token.Split('.');
        segments[2] = (segments[2][0] == 'A' ? 'B' : 'A') + segments[2][1..];

        Assert.Null(Reader.Read(ContextWithCookie(string.Join('.', segments))));
    }

    [Fact]
    public void Read_ATokenSignedWithAnotherSecret_ReturnsNull()
    {
        var other = new JwtService(
            Options.Create(new JwtOptions { Secret = "another-unit-test-secret-key-value" }),
            TimeProvider.System);

        Assert.Null(Reader.Read(ContextWithCookie(other.Sign(UserId, "ana@example.com"))));
    }

    [Fact]
    public void Read_AnExpiredToken_ReturnsNull()
    {
        var clock = new FixedTimeProvider();
        var jwt = new JwtService(Options.Create(new JwtOptions { Secret = "unit-test-jwt-secret-key-value", ExpiresInDays = 7 }), clock);
        var token = jwt.Sign(UserId, "ana@example.com");
        clock.Advance(TimeSpan.FromDays(8));

        Assert.Null(new SessionTokenReader(jwt).Read(ContextWithCookie(token)));
    }

    [Fact]
    public void Read_ReturnsTheCachedSessionEvenIfTheCookieChangesAfterwards()
    {
        var reader = Reader;
        var context = ContextWithCookie(_jwt.Sign(UserId, "ana@example.com"));
        var first = reader.Read(context);

        context.Request.Headers.Cookie = $"{SessionCookie.AccessTokenName}=garbage";

        Assert.Equal(first, reader.Read(context));
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
