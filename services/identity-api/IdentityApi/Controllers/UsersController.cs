using IdentityApi.DTOs;
using IdentityApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace IdentityApi.Controllers;

// Self-service profile. Authenticated at the Gateway (authorization policy); we trust the
// X-User-Id header it sets — identity-api has no auth scheme of its own.
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly ProfileService _service;
    public UsersController(ProfileService service) => _service = service;

    // ── GET /api/users/me ───────────────────────────────────────
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = CallerId();
        if (userId is null) return Unauthorized(new { error = "Not authenticated" });

        var result = await _service.GetAsync(userId.Value);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { error = result.Error });
    }

    // ── PUT /api/users/me ───────────────────────────────────────
    [HttpPut("me")]
    public async Task<IActionResult> Update([FromBody] UpdateProfileRequest request)
    {
        var userId = CallerId();
        if (userId is null) return Unauthorized(new { error = "Not authenticated" });

        var result = await _service.UpdateAsync(userId.Value, request);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    private Guid? CallerId()
        => Request.Headers.TryGetValue("X-User-Id", out var v) && Guid.TryParse(v.ToString(), out var id)
            ? id
            : null;
}
