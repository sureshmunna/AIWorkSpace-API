using AIWorkspace.API.DTOs;
using AIWorkspace.API.Models;
using AIWorkspace.API.Repositories;
using AIWorkspace.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIWorkspace.API.Controllers;

[ApiController]
[Authorize]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly IClaudeService _claude;
    private readonly IRepository<TaskItem> _tasks;
    private readonly IRepository<MeetingNote> _meetings;

    public AiController(IClaudeService claude, IRepository<TaskItem> tasks, IRepository<MeetingNote> meetings)
    {
        _claude = claude; _tasks = tasks; _meetings = meetings;
    }

    // POST /api/ai/prioritize  -> Claude suggests priority, a time estimate and subtasks for one task
    [HttpPost("prioritize")]
    public async Task<ActionResult<TaskItem>> Prioritize(PrioritizeDto dto, CancellationToken ct)
    {
        var task = await _tasks.GetAsync(User.UserId(), dto.TaskId);
        if (task is null) return NotFound();

        const string system =
            "You are a productivity assistant. Respond with ONLY a JSON object, no prose, in this exact shape: " +
            "{\"priority\":\"High|Medium|Low\",\"reason\":\"one short sentence\",\"estimateMinutes\":<integer>,\"subtasks\":[\"step\", ...]} " +
            "Give 2 to 5 concrete subtasks.";
        var prompt = $"Task title: {task.Title}\nDescription: {task.Description}\nDue date: {(task.DueDate?.ToString("yyyy-MM-dd") ?? "none")}\nToday: {DateTime.UtcNow:yyyy-MM-dd}";

        try
        {
            var json = AiHelpers.ExtractJson(await _claude.CompleteAsync(system, prompt, ct));
            var priority = json.Str("priority");
            var reason = json.Str("reason") ?? "";
            task.AIPrioritySuggestion = $"{priority ?? "Medium"} - {reason}".Trim(' ', '-');
            if (json.TryGetProperty("estimateMinutes", out var m) && m.TryGetInt32(out var mins) && mins > 0)
                task.AITimeEstimateMinutes = mins;
            task.AISubtasks = json.StrList("subtasks");
            if (priority is "High" or "Medium" or "Low") task.Priority = priority;

            await _tasks.UpdateAsync(task);
            return Ok(task);
        }
        catch (ClaudeException ex) { return Problem(ex.Message, statusCode: ex.StatusCode); }
    }

    // POST /api/ai/summarize  -> meeting notes in, summary + decisions + action items out (optionally creates tasks)
    [HttpPost("summarize")]
    public async Task<ActionResult<MeetingNote>> Summarize(SummarizeDto dto, CancellationToken ct)
    {
        const string system =
            "You summarise meeting notes. Respond with ONLY a JSON object, no prose, in this exact shape: " +
            "{\"summary\":\"2-4 sentences\",\"decisions\":[\"...\"],\"actionItems\":[{\"task\":\"...\",\"owner\":\"name or null\"}]}";
        try
        {
            var json = AiHelpers.ExtractJson(await _claude.CompleteAsync(system, dto.Notes, ct));
            var note = new MeetingNote
            {
                UserId = User.UserId(),
                Title = string.IsNullOrWhiteSpace(dto.Title) ? "Untitled meeting" : dto.Title.Trim(),
                RawNotes = dto.Notes,
                Summary = json.Str("summary") ?? "",
                Decisions = json.StrList("decisions")
            };
            if (json.TryGetProperty("actionItems", out var items) && items.ValueKind == System.Text.Json.JsonValueKind.Array)
                foreach (var i in items.EnumerateArray())
                {
                    var t = i.Str("task");
                    if (!string.IsNullOrWhiteSpace(t)) note.ActionItems.Add(new ActionItem { Task = t!, Owner = i.Str("owner") });
                }

            await _meetings.AddAsync(note);

            if (dto.CreateTasks)
                foreach (var a in note.ActionItems)
                    await _tasks.AddAsync(new TaskItem
                    {
                        UserId = note.UserId,
                        Title = a.Task,
                        Description = $"From meeting: {note.Title}" + (a.Owner is null ? "" : $" (owner: {a.Owner})")
                    });

            return Ok(note);
        }
        catch (ClaudeException ex) { return Problem(ex.Message, statusCode: ex.StatusCode); }
    }
}
