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
public class TasksController : ControllerBase
{
    private static readonly string[] Priorities = { "Low", "Medium", "High" };
    private readonly IRepository<TaskItem> _repo;

    public TasksController(IRepository<TaskItem> repo) => _repo = repo;

    [HttpGet]
    public async Task<ActionResult<List<TaskItem>>> GetAll() => Ok(await _repo.ListAsync(User.UserId()));

    [HttpGet("{id}")]
    public async Task<ActionResult<TaskItem>> Get(string id)
    {
        var t = await _repo.GetAsync(User.UserId(), id);
        return t is null ? NotFound() : Ok(t);
    }

    [HttpPost]
    public async Task<ActionResult<TaskItem>> Create(CreateTaskDto dto)
    {
        var priority = NormalizePriority(dto.Priority) ?? "Medium";
        var t = new TaskItem
        {
            UserId = User.UserId(),
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim() ?? "",
            Priority = priority,
            DueDate = dto.DueDate
        };
        await _repo.AddAsync(t);
        return CreatedAtAction(nameof(Get), new { id = t.Id }, t);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TaskItem>> Update(string id, UpdateTaskDto dto)
    {
        var t = await _repo.GetAsync(User.UserId(), id);
        if (t is null) return NotFound();

        if (dto.Title is not null) t.Title = dto.Title.Trim();
        if (dto.Description is not null) t.Description = dto.Description.Trim();
        if (dto.Priority is not null)
        {
            var p = NormalizePriority(dto.Priority);
            if (p is null) return BadRequest("Priority must be Low, Medium or High.");
            t.Priority = p;
        }
        if (dto.IsCompleted is not null) t.IsCompleted = dto.IsCompleted.Value;
        if (dto.DueDate is not null) t.DueDate = dto.DueDate;

        await _repo.UpdateAsync(t);
        return Ok(t);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id) =>
        await _repo.DeleteAsync(User.UserId(), id) ? NoContent() : NotFound();

    private static string? NormalizePriority(string? p) =>
        Priorities.FirstOrDefault(x => string.Equals(x, p?.Trim(), StringComparison.OrdinalIgnoreCase));
}
