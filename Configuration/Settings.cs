namespace AIWorkspace.API.Configuration;

public class JwtSettings
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "AIWorkspaceAPI";
    public string Audience { get; set; } = "AIWorkspaceClient";
    public int ExpiryInHours { get; set; } = 24;
}

public class SupabaseSettings
{
    public string Url { get; set; } = "";
    public string PublishableKey { get; set; } = "";
}

public class MongoDBConfig
{
    public string ConnectionString { get; set; } = "";
    public string DatabaseName { get; set; } = "AIWorkspace";
}

public class ClaudeSettings
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "claude-sonnet-4-6";
    public int MaxTokens { get; set; } = 1000;
    public string BaseUrl { get; set; } = "https://api.anthropic.com";
}

/// <summary>Switches that decide which implementation is plugged in.</summary>
public class ModeSettings
{
    /// <summary>"Local" = in-memory users (works offline). "Supabase" = real Supabase Auth.</summary>
    public string Auth { get; set; } = "Local";
    /// <summary>"Memory" = lost on restart. "Mongo" = persisted in MongoDB Atlas.</summary>
    public string Storage { get; set; } = "Memory";
}
