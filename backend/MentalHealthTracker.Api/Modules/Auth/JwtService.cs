using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MentalHealthTracker.Api.Core.Configuration;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.Api.Modules.Auth;

public sealed class JwtService
{
    public const string SessionCookieName = "access_token";

    public static readonly TimeSpan SessionCookieMaxAge = TimeSpan.FromDays(7);

    private const string Algorithm = "HS256";

    private const string HeaderJson = """{"alg":"HS256","typ":"JWT"}""";

    private readonly byte[] _key;

    private readonly int _expiresInDays;

    public JwtService(IOptions<JwtOptions> options)
    {
        _key = Encoding.UTF8.GetBytes(options.Value.Secret);
        _expiresInDays = options.Value.ExpiresInDays;
    }

    public string Sign(Guid userId, string email)
    {
        var now = DateTimeOffset.UtcNow;
        var claims = new Dictionary<string, object>
        {
            ["sub"] = userId.ToString(),
            ["email"] = email,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddDays(_expiresInDays).ToUnixTimeSeconds(),
        };

        var header = Encode(Encoding.UTF8.GetBytes(HeaderJson));
        var payload = Encode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(claims)));

        var signingInput = $"{header}.{payload}";
        var signature = Encode(ComputeSignature(Encoding.UTF8.GetBytes(signingInput)));

        return $"{signingInput}.{signature}";
    }

    public (Guid UserId, string Email)? Verify(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var segments = token.Split('.');
        if (segments.Length != 3)
        {
            return null;
        }

        try
        {
            using var headerJson = JsonDocument.Parse(Decode(segments[0]));
            if (!headerJson.RootElement.TryGetProperty("alg", out var algorithm) ||
                algorithm.GetString() != Algorithm)
            {
                return null;
            }

            var signingInput = $"{segments[0]}.{segments[1]}";
            var expectedSignature = ComputeSignature(Encoding.UTF8.GetBytes(signingInput));
            var actualSignature = Decode(segments[2]);
            if (!CryptographicOperations.FixedTimeEquals(expectedSignature, actualSignature))
            {
                return null;
            }

            using var payloadJson = JsonDocument.Parse(Decode(segments[1]));
            var root = payloadJson.RootElement;

            if (!root.TryGetProperty("sub", out var subElement) ||
                !Guid.TryParse(subElement.GetString(), out var userId))
            {
                return null;
            }

            if (!root.TryGetProperty("email", out var emailElement) ||
                string.IsNullOrEmpty(emailElement.GetString()))
            {
                return null;
            }

            if (!root.TryGetProperty("exp", out var expiresAtElement) ||
                !expiresAtElement.TryGetInt64(out var expiresAt))
            {
                return null;
            }

            if (expiresAt < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                return null;
            }

            return (userId, emailElement.GetString()!);
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private byte[] ComputeSignature(byte[] data)
    {
        using var hmac = new HMACSHA256(_key);
        return hmac.ComputeHash(data);
    }

    private static string Encode(byte[] data) =>
        Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

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