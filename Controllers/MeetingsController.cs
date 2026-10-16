using AIWorkspace.API.Models;
using AIWorkspace.API.Repositories;
using AIWorkspace.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIWorkspace.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class MeetingsController : ControllerBase
{
    private readonly IRepository<MeetingNote> _repo;
    public MeetingsController(IRepository<MeetingNote> repo) => _repo = repo;

    [HttpGet]
    public async Task<ActionResult<List<MeetingNote>>> GetAll() => Ok(await _repo.ListAsync(User.UserId()));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id) =>
        await _repo.DeleteAsync(User.UserId(), id) ? NoContent() : NotFound();
}
