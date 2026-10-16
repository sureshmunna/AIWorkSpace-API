using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIWorkspace.API.Configuration;
using Microsoft.Extensions.Options;

namespace AIWorkspace.API.Services;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) Create(string userId, string email, string fullName);
}

/// <summary>
/// Builds an HS256 JWT by hand so the three parts are visible: base64url(header).base64url(payload).base64url(HMACSHA256(secret, header.payload)).
/// The JwtBearer middleware in Program.cs validates it (signature, issuer, audience, expiry).
/// </summary>
public class JwtTokenService : ITokenService
{
    private readonly JwtSettings _jwt;
    public JwtTokenService(IOptions<JwtSettings> jwt) => _jwt = jwt.Value;

    public (string Token, DateTime ExpiresAt) Create(string userId, string email, string fullName)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddHours(_jwt.ExpiryInHours);

        var header = new Dictionary<string, object> { ["alg"] = "HS256", ["typ"] = "JWT" };
        var payload = new Dictionary<string, object>
        {
            ["sub"] = userId,
            ["email"] = email,
            ["name"] = fullName,
            ["jti"] = Guid.NewGuid().ToString(),
            ["iss"] = _jwt.Issuer,
            ["aud"] = _jwt.Audience,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["nbf"] = now.ToUnixTimeSeconds(),
            ["exp"] = expires.ToUnixTimeSeconds()
        };

        var signingInput = B64(JsonSerializer.SerializeToUtf8Bytes(header)) + "." + B64(JsonSerializer.SerializeToUtf8Bytes(payload));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_jwt.Key));
        var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput));
        return (signingInput + "." + B64(sig), expires.UtcDateTime);
    }

    private static string B64(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public static class UserExtensions
{
    // Token claim names are kept as-is ("sub", "email", "name"): Program.cs sets MapInboundClaims = false.
    public static string UserId(this ClaimsPrincipal u) => u.FindFirst("sub")?.Value ?? "";
    public static string FullName(this ClaimsPrincipal u) => u.FindFirst("name")?.Value ?? "";
}
