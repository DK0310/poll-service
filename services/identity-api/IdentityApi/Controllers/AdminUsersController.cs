using IdentityApi.DTOs;
using IdentityApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace IdentityApi.Controllers;

// Admin-only user management. The gateway already restricts /api/admin/** to admins; re-checking
// X-User-Role here is defense-in-depth (identity-api has no auth scheme, it trusts gateway headers).
[ApiController]
[Route("api/admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly AdminService _service;
    public AdminUsersController(AdminService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List()
    {
        if (!IsAdmin()) return Forbidden();
        return Ok(await _service.ListUsersAsync());
    }

    [HttpPost("{id:guid}/role")]
    public async Task<IActionResult> SetRole(Guid id, [FromBody] SetRoleRequest request)
    {
        if (!IsAdmin()) return Forbidden();
        var result = await _service.SetRoleAsync(id, request.Role, CallerId());
        if (result.IsSuccess) return Ok(result.Value);
        return result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? NotFound(new { error = result.Error })
            : BadRequest(new { error = result.Error });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!IsAdmin()) return Forbidden();
        var result = await _service.DeleteUserAsync(id, CallerId());
        if (result.IsSuccess) return NoContent();
        return result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? NotFound(new { error = result.Error })
            : BadRequest(new { error = result.Error });
    }

    private IActionResult Forbidden()
        => StatusCode(StatusCodes.Status403Forbidden, new { error = "Admin only" });

    private bool IsAdmin()
        => Request.Headers.TryGetValue("X-User-Role", out var role) && role.ToString() == "Admin";

    private Guid? CallerId()
        => Request.Headers.TryGetValue("X-User-Id", out var v) && Guid.TryParse(v.ToString(), out var id)
            ? id
            : null;
}
