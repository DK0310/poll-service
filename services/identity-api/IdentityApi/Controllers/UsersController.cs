using IdentityApi.DTOs;
using IdentityApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace IdentityApi.Controllers;

// Self-service profile. Auth is enforced at the gateway; we read the caller from X-User-Id
// (identity-api has no auth scheme of its own).
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly ProfileService _service;
    public UsersController(ProfileService service) => _service = service;

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
