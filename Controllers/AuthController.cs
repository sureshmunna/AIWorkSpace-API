using AIWorkspace.API.DTOs;
using AIWorkspace.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIWorkspace.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly ITokenService _tokens;

    public AuthController(IAuthService auth, ITokenService tokens) { _auth = auth; _tokens = tokens; }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto)
    {
        try { return Ok(ToResponse(await _auth.RegisterAsync(dto))); }
        catch (AuthException ex) { return Problem(ex.Message, statusCode: ex.StatusCode); }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
    {
        try { return Ok(ToResponse(await _auth.LoginAsync(dto))); }
        catch (AuthException ex) { return Problem(ex.Message, statusCode: ex.StatusCode); }
    }

    private AuthResponseDto ToResponse(AuthUser u)
    {
        var (token, expires) = _tokens.Create(u.Id, u.Email, u.FullName);
        return new AuthResponseDto { Token = token, UserId = u.Id, Email = u.Email, FullName = u.FullName, ExpiresAt = expires };
    }
}
