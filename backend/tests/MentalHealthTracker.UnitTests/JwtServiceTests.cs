using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MentalHealthTracker.Api.Core.Configuration;
using MentalHealthTracker.Api.Modules.Auth;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.UnitTests;

// Pruebas de caracterización: congelan el comportamiento actual de JwtService antes de
// tocarlo. Los tokens "malos" se construyen a mano con el mismo secreto y la misma
// codificación base64url, de modo que cada caso viola UNA sola cosa y el null se debe a
// ella; el test de control confirma que el constructor de tokens produce uno válido.
public sealed class JwtServiceTests
{
    private const string Secret = "unit-test-jwt-secret-key-value";

    private static readonly Guid UserId = Guid.Parse("3f2c1a9e-7b4d-4e8a-9c55-0d6e2b1f4a73");

    private static JwtService CreateService(string secret = Secret, int expiresInDays = 7) =>
        new(Options.Create(new JwtOptions { Secret = secret, ExpiresInDays = expiresInDays }));

    [Fact]
    public void SignThenVerify_ReturnsTheSameUserAndEmail()
    {
        var service = CreateService();

        var result = service.Verify(service.Sign(UserId, "ana@example.com"));

        Assert.Equal((UserId, "ana@example.com"), result);
    }

    [Fact]
    public void Sign_ProducesHs256TokenWithThreeSegments()
    {
        var token = CreateService().Sign(UserId, "ana@example.com");

        var segments = token.Split('.');
        Assert.Equal(3, segments.Length);
        using var header = JsonDocument.Parse(Decode(segments[0]));
        Assert.Equal("HS256", header.RootElement.GetProperty("alg").GetString());
        Assert.Equal("JWT", header.RootElement.GetProperty("typ").GetString());
        using var payload = JsonDocument.Parse(Decode(segments[1]));
        Assert.Equal(UserId.ToString(), payload.RootElement.GetProperty("sub").GetString());
        Assert.Equal("ana@example.com", payload.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public void Sign_SetsExpirationFromExpiresInDays()
    {
        var before = DateTimeOffset.UtcNow;
        var token = CreateService(expiresInDays: 3).Sign(UserId, "ana@example.com");
        var after = DateTimeOffset.UtcNow;

        using var payload = JsonDocument.Parse(Decode(token.Split('.')[1]));
        var exp = payload.RootElement.GetProperty("exp").GetInt64();
        Assert.InRange(exp, before.AddDays(3).ToUnixTimeSeconds(), after.AddDays(3).ToUnixTimeSeconds());
    }

    [Fact]
    public void SessionLifetime_WithDefaultOptions_Is604800Seconds()
    {
        var service = new JwtService(Options.Create(new JwtOptions { Secret = "unit-test-jwt-secret-key-value" }));

        Assert.Equal(604800, service.SessionLifetime.TotalSeconds);
    }

    [Fact]
    public void SessionLifetime_FollowsExpiresInDays()
    {
        Assert.Equal(TimeSpan.FromDays(3), CreateService(expiresInDays: 3).SessionLifetime);
    }

    [Fact]
    public void Verify_HandBuiltValidToken_IsAccepted()
    {
        // Control: el constructor de tokens de abajo es válido, así que los null de los
        // demás casos se deben a lo que cada uno rompe y no al constructor.
        var token = BuildToken();

        Assert.Equal((UserId, "ana@example.com"), CreateService().Verify(token));
    }

    [Fact]
    public void Verify_TamperedSignature_ReturnsNull()
    {
        var token = BuildToken();
        var segments = token.Split('.');
        // Se cambia el PRIMER carácter de la firma: alterar el último puede dejar los
        // mismos bytes (los bits sobrantes del base64 se ignoran) y no sería una alteración.
        var signature = segments[2];
        var tampered = (signature[0] == 'A' ? 'B' : 'A') + signature[1..];

        Assert.Null(CreateService().Verify($"{segments[0]}.{segments[1]}.{tampered}"));
    }

    [Fact]
    public void Verify_TamperedPayload_ReturnsNull()
    {
        var segments = BuildToken().Split('.');
        var otherPayload = Encode(Json(new Dictionary<string, object>
        {
            ["sub"] = Guid.NewGuid().ToString(),
            ["email"] = "intruder@example.com",
            ["exp"] = FutureExp(),
        }));

        Assert.Null(CreateService().Verify($"{segments[0]}.{otherPayload}.{segments[2]}"));
    }

    [Fact]
    public void Verify_TokenSignedWithAnotherSecret_ReturnsNull()
    {
        var token = BuildToken(secret: "another-secret-key-value-xyz");

        Assert.Null(CreateService().Verify(token));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("HS512")]
    [InlineData("hs256")]
    public void Verify_AlgorithmOtherThanHs256_ReturnsNull(string algorithm)
    {
        // La firma es válida para ese encabezado: lo único que falla es el alg.
        var token = BuildToken(header: $$"""{"alg":"{{algorithm}}","typ":"JWT"}""");

        Assert.Null(CreateService().Verify(token));
    }

    [Fact]
    public void Verify_HeaderWithoutAlg_ReturnsNull()
    {
        var token = BuildToken(header: """{"typ":"JWT"}""");

        Assert.Null(CreateService().Verify(token));
    }

    [Fact]
    public void Verify_ExpiredToken_ReturnsNull()
    {
        var token = BuildToken(exp: DateTimeOffset.UtcNow.AddSeconds(-10).ToUnixTimeSeconds());

        Assert.Null(CreateService().Verify(token));
    }

    [Fact]
    public void Verify_TokenWithoutExp_ReturnsNull()
    {
        var token = BuildPayloadToken(new Dictionary<string, object>
        {
            ["sub"] = UserId.ToString(),
            ["email"] = "ana@example.com",
        });

        Assert.Null(CreateService().Verify(token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("onlyonesegment")]
    [InlineData("two.segments")]
    public void Verify_FewerThanThreeSegments_ReturnsNull(string token)
    {
        Assert.Null(CreateService().Verify(token));
    }

    [Fact]
    public void Verify_NullToken_ReturnsNull()
    {
        Assert.Null(CreateService().Verify(null));
    }

    [Fact]
    public void Verify_MoreThanThreeSegments_ReturnsNull()
    {
        Assert.Null(CreateService().Verify(BuildToken() + ".extra"));
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    public void Verify_SubThatIsNotAGuid_ReturnsNull(string sub)
    {
        var token = BuildToken(sub: sub);

        Assert.Null(CreateService().Verify(token));
    }

    [Fact]
    public void Verify_TokenWithoutSub_ReturnsNull()
    {
        var token = BuildPayloadToken(new Dictionary<string, object>
        {
            ["email"] = "ana@example.com",
            ["exp"] = FutureExp(),
        });

        Assert.Null(CreateService().Verify(token));
    }

    [Fact]
    public void Verify_EmptyEmail_ReturnsNull()
    {
        var token = BuildToken(email: "");

        Assert.Null(CreateService().Verify(token));
    }

    [Fact]
    public void Verify_TokenWithoutEmail_ReturnsNull()
    {
        var token = BuildPayloadToken(new Dictionary<string, object>
        {
            ["sub"] = UserId.ToString(),
            ["exp"] = FutureExp(),
        });

        Assert.Null(CreateService().Verify(token));
    }

    [Theory]
    [InlineData("!!!.###.$$$")]
    [InlineData("a.b.c")]
    public void Verify_GarbageSegments_ReturnsNullWithoutThrowing(string token)
    {
        Assert.Null(CreateService().Verify(token));
    }

    // --- constructor de tokens a mano, con el mismo secreto y la misma codificación ---

    private static long FutureExp() => DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();

    private static string BuildToken(
        string secret = Secret,
        string header = """{"alg":"HS256","typ":"JWT"}""",
        string? sub = null,
        string email = "ana@example.com",
        long? exp = null) =>
        BuildPayloadToken(
            new Dictionary<string, object>
            {
                ["sub"] = sub ?? UserId.ToString(),
                ["email"] = email,
                ["exp"] = exp ?? FutureExp(),
            },
            secret,
            header);

    private static string BuildPayloadToken(
        Dictionary<string, object> claims,
        string secret = Secret,
        string header = """{"alg":"HS256","typ":"JWT"}""")
    {
        var signingInput = $"{Encode(Encoding.UTF8.GetBytes(header))}.{Encode(Json(claims))}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signature = Encode(hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput)));
        return $"{signingInput}.{signature}";
    }

    private static byte[] Json(Dictionary<string, object> claims) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(claims));

    private static string Encode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Decode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = (base64.Length % 4) switch
        {
            2 => base64 + "==",
            3 => base64 + "=",
            _ => base64,
        };
        return Convert.FromBase64String(base64);
    }
}
