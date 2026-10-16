namespace AIWorkspace.API.Models;

/// <summary>Every stored document belongs to one user. Property "Id" is mapped to Mongo's _id by convention.</summary>
public abstract class UserDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class TaskItem : UserDocument
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Priority { get; set; } = "Medium";   // Low | Medium | High
    public bool IsCompleted { get; set; }
    public DateTime? DueDate { get; set; }

    // Filled in by Claude
    public string? AIPrioritySuggestion { get; set; }
    public int? AITimeEstimateMinutes { get; set; }
    public List<string> AISubtasks { get; set; } = new();
}

public class ChatMessage : UserDocument
{
    public string Role { get; set; } = "user";           // user | assistant
    public string Content { get; set; } = "";
}

public class ActionItem
{
    public string Task { get; set; } = "";
    public string? Owner { get; set; }
}

public class MeetingNote : UserDocument
{
    public string Title { get; set; } = "";
    public string RawNotes { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<string> Decisions { get; set; } = new();
    public List<ActionItem> ActionItems { get; set; } = new();
}
