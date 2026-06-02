using IdentityApi.DTOs;
using IdentityApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace IdentityApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _service;
    public AuthController(AuthService service) => _service = service;

    // ── POST /api/auth/register ─────────────────────────────────
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _service.RegisterAsync(request);
        return result.IsSuccess
            ? Ok(new AuthResponse { Token = result.Value! })
            : BadRequest(new { error = result.Error });
    }

    // ── POST /api/auth/login ────────────────────────────────────
    // Bad credentials return 400 (not 401) so the frontend's global 401 handler
    // doesn't hijack a failed login attempt.
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _service.LoginAsync(request);
        return result.IsSuccess
            ? Ok(new AuthResponse { Token = result.Value! })
            : BadRequest(new { error = result.Error });
    }
}
