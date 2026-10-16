using System.Text;
using AIWorkspace.API.DTOs;
using AIWorkspace.API.Models;
using AIWorkspace.API.Repositories;
using AIWorkspace.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIWorkspace.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private const int HistoryWindow = 20;
    private readonly IClaudeService _claude;
    private readonly IRepository<ChatMessage> _chat;
    private readonly IRepository<TaskItem> _tasks;

    public ChatController(IClaudeService claude, IRepository<ChatMessage> chat, IRepository<TaskItem> tasks)
    {
        _claude = claude; _chat = chat; _tasks = tasks;
    }

    // GET /api/chat  -> history, oldest first
    [HttpGet]
    public async Task<ActionResult<List<ChatMessage>>> History()
    {
        var list = await _chat.ListAsync(User.UserId(), 100);
        list.Reverse();
        return Ok(list);
    }

    [HttpDelete]
    public async Task<IActionResult> Clear()
    {
        await _chat.DeleteAllAsync(User.UserId());
        return NoContent();
    }

    // POST /api/chat/stream  -> streams Claude's reply as plain text chunks
    [HttpPost("stream")]
    public async Task Stream(ChatRequestDto dto, CancellationToken ct)
    {
        var userId = User.UserId();

        // Build context: system prompt (with the user's open tasks) + recent history + the new message.
        var openTasks = (await _tasks.ListAsync(userId)).Where(t => !t.IsCompleted).Take(15).ToList();
        var system = new StringBuilder("You are a helpful workspace assistant inside a task-management app. Be concise.");
        if (openTasks.Count > 0)
        {
            system.Append("\nThe user's open tasks:");
            foreach (var t in openTasks) system.Append($"\n- [{t.Priority}] {t.Title}");
        }

        var recent = await _chat.ListAsync(userId, HistoryWindow);
        recent.Reverse();
        var messages = recent.Select(m => new LlmMessage(m.Role, m.Content)).ToList();
        messages.Add(new LlmMessage("user", dto.Message));

        // Open the stream BEFORE sending headers so a config/API error can still become a clean JSON error response.
        var enumerator = _claude.StreamAsync(system.ToString(), messages, ct).GetAsyncEnumerator(ct);
        bool hasFirst;
        try { hasFirst = await enumerator.MoveNextAsync(); }
        catch (ClaudeException ex)
        {
            await enumerator.DisposeAsync();
            Response.StatusCode = ex.StatusCode;
            Response.ContentType = "application/problem+json";
            await Response.WriteAsJsonAsync(new { title = ex.Message, status = ex.StatusCode }, ct);
            return;
        }

        await _chat.AddAsync(new ChatMessage { UserId = userId, Role = "user", Content = dto.Message });

        Response.ContentType = "text/plain; charset=utf-8";
        Response.Headers.CacheControl = "no-cache";
        var full = new StringBuilder();
        try
        {
            if (hasFirst)
            {
                do
                {
                    full.Append(enumerator.Current);
                    await Response.WriteAsync(enumerator.Current, ct);
                    await Response.Body.FlushAsync(ct);
                } while (await enumerator.MoveNextAsync());
            }
        }
        catch (ClaudeException ex)
        {
            await Response.WriteAsync("\n\n[Error: " + ex.Message + "]", CancellationToken.None);
        }
        catch (OperationCanceledException) { /* client disconnected: save what we have */ }
        finally { await enumerator.DisposeAsync(); }

        if (full.Length > 0)
            await _chat.AddAsync(new ChatMessage { UserId = userId, Role = "assistant", Content = full.ToString() });
    }
}
