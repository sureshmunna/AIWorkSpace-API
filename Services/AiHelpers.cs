using System.Text.Json;

namespace AIWorkspace.API.Services;

public static class AiHelpers
{
    /// <summary>Models sometimes wrap JSON in prose or ``` fences. Pull out the first balanced {...} object.</summary>
    public static JsonElement ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0) throw new ClaudeException("Claude did not return JSON.");
        int depth = 0; bool inStr = false, esc = false;
        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (inStr)
            {
                if (esc) esc = false;
                else if (c == '\\') esc = true;
                else if (c == '"') inStr = false;
                continue;
            }
            if (c == '"') inStr = true;
            else if (c == '{') depth++;
            else if (c == '}' && --depth == 0)
            {
                try { return JsonDocument.Parse(text[start..(i + 1)]).RootElement.Clone(); }
                catch (JsonException) { throw new ClaudeException("Claude returned malformed JSON."); }
            }
        }
        throw new ClaudeException("Claude returned incomplete JSON.");
    }

    public static string? Str(this JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static List<string> StrList(this JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
            : new List<string>();
}
