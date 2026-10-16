using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using AIWorkspace.API.Configuration;
using AIWorkspace.API.DTOs;
using Microsoft.Extensions.Options;

namespace AIWorkspace.API.Services;

public class AuthException : Exception
{
    public int StatusCode { get; }
    public AuthException(string message, int statusCode = 400) : base(message) => StatusCode = statusCode;
}

public record AuthUser(string Id, string Email, string FullName);

public interface IAuthService
{
    Task<AuthUser> RegisterAsync(RegisterDto dto);
    Task<AuthUser> LoginAsync(LoginDto dto);
}

/// <summary>Users kept in memory with PBKDF2-hashed passwords. Lets the whole app run with no external service.</summary>
public class LocalAuthService : IAuthService
{
    private record Account(AuthUser User, byte[] Salt, byte[] Hash);
    private static readonly ConcurrentDictionary<string, Account> Accounts = new();

    private static byte[] Hash(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);

    public Task<AuthUser> RegisterAsync(RegisterDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var salt = RandomNumberGenerator.GetBytes(16);
        var acc = new Account(new AuthUser(Guid.NewGuid().ToString(), email, dto.FullName.Trim()), salt, Hash(dto.Password, salt));
        if (!Accounts.TryAdd(email, acc)) throw new AuthException("An account with this email already exists.", 409);
        return Task.FromResult(acc.User);
    }

    public Task<AuthUser> LoginAsync(LoginDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        if (!Accounts.TryGetValue(email, out var acc) ||
            !CryptographicOperations.FixedTimeEquals(acc.Hash, Hash(dto.Password, acc.Salt)))
            throw new AuthException("Invalid email or password.", 401);
        return Task.FromResult(acc.User);
    }
}

/// <summary>Delegates user storage and password checking to Supabase Auth over REST.</summary>
public class SupabaseAuthService : IAuthService
{
    private readonly HttpClient _http;
    private readonly SupabaseSettings _cfg;

    public SupabaseAuthService(IHttpClientFactory factory, IOptions<SupabaseSettings> cfg)
    {
        _http = factory.CreateClient("Supabase");
        _cfg = cfg.Value;
    }

    public async Task<AuthUser> RegisterAsync(RegisterDto dto)
    {
        var body = new { email = dto.Email, password = dto.Password, data = new { full_name = dto.FullName } };
        var json = await PostAsync("/auth/v1/signup", body);
        // With "Confirm email" OFF Supabase returns { user: {...} }; with it ON it returns the user object itself.
        var user = json.TryGetProperty("user", out var u) ? u : json;
        return ReadUser(user, dto.Email, dto.FullName);
    }

    public async Task<AuthUser> LoginAsync(LoginDto dto)
    {
        var json = await PostAsync("/auth/v1/token?grant_type=password", new { email = dto.Email, password = dto.Password });
        var user = json.GetProperty("user");
        return ReadUser(user, dto.Email, "");
    }

    private static AuthUser ReadUser(JsonElement user, string fallbackEmail, string fallbackName)
    {
        var id = user.GetProperty("id").GetString()!;
        var email = user.TryGetProperty("email", out var e) ? e.GetString() ?? fallbackEmail : fallbackEmail;
        var name = fallbackName;
        if (user.TryGetProperty("user_metadata", out var meta) && meta.ValueKind == JsonValueKind.Object &&
            meta.TryGetProperty("full_name", out var fn) && !string.IsNullOrWhiteSpace(fn.GetString()))
            name = fn.GetString()!;
        return new AuthUser(id, email, string.IsNullOrWhiteSpace(name) ? email : name);
    }

    private async Task<JsonElement> PostAsync(string path, object body)
    {
        HttpResponseMessage res;
        try
        {
            res = await _http.PostAsJsonAsync(_cfg.Url.TrimEnd('/') + path, body);
        }
        catch (HttpRequestException ex)
        {
            throw new AuthException(
                "Could not reach Supabase (" + ex.Message + "). If you are on an Indian ISP, supabase.co may be blocked: " +
                "switch DNS to 1.1.1.1 / 8.8.8.8, use a VPN, or set Modes:Auth to \"Local\".", 503);
        }

        var text = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
        {
            string msg = "Authentication failed.";
            try
            {
                using var d = JsonDocument.Parse(text);
                foreach (var key in new[] { "msg", "error_description", "message" })
                    if (d.RootElement.TryGetProperty(key, out var m) && m.ValueKind == JsonValueKind.String) { msg = m.GetString()!; break; }
            }
            catch (JsonException) { }
            throw new AuthException(msg, (int)res.StatusCode is >= 400 and < 500 ? (int)res.StatusCode : 502);
        }
        return JsonDocument.Parse(text).RootElement.Clone();
    }
}
