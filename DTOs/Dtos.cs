using System.ComponentModel.DataAnnotations;

namespace AIWorkspace.API.DTOs;

public class RegisterDto
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, MinLength(6)] public string Password { get; set; } = "";
    [Required] public string FullName { get; set; } = "";
}

public class LoginDto
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
}

public class AuthResponseDto
{
    public string Token { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}

public class CreateTaskDto
{
    [Required, MaxLength(200)] public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? Priority { get; set; }
    public DateTime? DueDate { get; set; }
}

public class UpdateTaskDto
{
    [MaxLength(200)] public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Priority { get; set; }
    public bool? IsCompleted { get; set; }
    public DateTime? DueDate { get; set; }
}

public class PrioritizeDto { [Required] public string TaskId { get; set; } = ""; }

public class SummarizeDto
{
    [MaxLength(200)] public string? Title { get; set; }
    [Required, MinLength(10)] public string Notes { get; set; } = "";
    public bool CreateTasks { get; set; }
}

public class ChatRequestDto { [Required, MaxLength(4000)] public string Message { get; set; } = ""; }
