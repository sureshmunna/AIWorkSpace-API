using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AIWorkspace.API.Configuration;
using Microsoft.Extensions.Options;

namespace AIWorkspace.API.Services;

public class ClaudeException : Exception
{
    public int StatusCode { get; }
    public ClaudeException(string message, int statusCode = 502) : base(message) => StatusCode = statusCode;
}

public record LlmMessage(string Role, string Content);

public interface IClaudeService
{
    Task<string> CompleteAsync(string system, string userPrompt, CancellationToken ct = default);
    IAsyncEnumerable<string> StreamAsync(string system, IEnumerable<LlmMessage> messages, CancellationToken ct = default);
}

/// <summary>Calls the Anthropic Messages API directly (no SDK). The API key never leaves the server.</summary>
public class ClaudeService : IClaudeService
{
    private readonly HttpClient _http;
    private readonly ClaudeSettings _cfg;

    public ClaudeService(IHttpClientFactory factory, IOptions<ClaudeSettings> cfg)
    {
        _http = factory.CreateClient("Claude");
        _cfg = cfg.Value;
    }

    private HttpRequestMessage BuildRequest(string system, IEnumerable<LlmMessage> messages, bool stream)
    {
        if (string.IsNullOrWhiteSpace(_cfg.ApiKey))
            throw new ClaudeException("Claude API key is not configured. Set Claude:ApiKey (use dotnet user-secrets).", 503);

        var body = new
        {
            model = _cfg.Model,
            max_tokens = _cfg.MaxTokens,
            system,
            stream,
            messages = messages.Select(m => new { role = m.Role, content = m.Content })
        };
        var req = new HttpRequestMessage(HttpMethod.Post, _cfg.BaseUrl.TrimEnd('/') + "/v1/messages")
        {
            Content = JsonContent.Create(body)
        };
        req.Headers.Add("x-api-key", _cfg.ApiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");
        return req;
    }

    private static async Task<ClaudeException> ToException(HttpResponseMessage res)
    {
        var text = await res.Content.ReadAsStringAsync();
        var msg = $"Claude API returned {(int)res.StatusCode}.";
        try
        {
            using var d = JsonDocument.Parse(text);
            if (d.RootElement.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var m))
                msg = m.GetString() ?? msg;
        }
        catch (JsonException) { }
        return new ClaudeException(msg, 502);
    }

    public async Task<string> CompleteAsync(string system, string userPrompt, CancellationToken ct = default)
    {
        using var req = BuildRequest(system, new[] { new LlmMessage("user", userPrompt) }, stream: false);
        HttpResponseMessage res;
        try { res = await _http.SendAsync(req, ct); }
        catch (HttpRequestException ex) { throw new ClaudeException("Could not reach the Claude API: " + (ex.InnerException?.Message ?? ex.Message), 502); }

        using (res)
        {
            if (!res.IsSuccessStatusCode) throw await ToException(res);
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var sb = new System.Text.StringBuilder();
            foreach (var block in doc.RootElement.GetProperty("content").EnumerateArray())
                if (block.TryGetProperty("type", out var t) && t.GetString() == "text")
                    sb.Append(block.GetProperty("text").GetString());
            return sb.ToString();
        }
    }

    public async IAsyncEnumerable<string> StreamAsync(string system, IEnumerable<LlmMessage> messages,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var req = BuildRequest(system, messages, stream: true);
        HttpResponseMessage res;
        try { res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct); }
        catch (HttpRequestException ex) { throw new ClaudeException("Could not reach the Claude API: " + (ex.InnerException?.Message ?? ex.Message), 502); }

        using (res)
        {
            if (!res.IsSuccessStatusCode) throw await ToException(res);

            using var stream = await res.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);
            string? line;
            // Server-sent events: lines like  data: {"type":"content_block_delta","delta":{"type":"text_delta","text":"Hi"}}
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                if (!line.StartsWith("data:")) continue;
                var payload = line[5..].Trim();
                if (payload.Length == 0 || payload == "[DONE]") continue;

                string? chunk = null;
                try
                {
                    using var d = JsonDocument.Parse(payload);
                    var root = d.RootElement;
                    var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
                    if (type == "content_block_delta" &&
                        root.GetProperty("delta").TryGetProperty("text", out var txt))
                        chunk = txt.GetString();
                    else if (type == "error")
                        throw new ClaudeException(root.GetProperty("error").GetProperty("message").GetString() ?? "Claude stream error.");
                }
                catch (JsonException) { /* ignore malformed keep-alive lines */ }

                if (!string.IsNullOrEmpty(chunk)) yield return chunk;
            }
        }
    }
}
