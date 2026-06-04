using Microsoft.AspNetCore.Mvc;
using PollApi.Services;

namespace PollApi.Controllers;

// Admin-only poll administration. The Gateway gates /api/admin/** with the "admin"
// policy; this controller re-checks X-User-Role as defense-in-depth (poll-api has no
// auth scheme of its own — it trusts the Gateway-set headers).
[ApiController]
[Route("api/admin/polls")]
public class AdminPollsController : ControllerBase
{
    private readonly PollService _service;
    public AdminPollsController(PollService service) => _service = service;

    // ── GET /api/admin/polls ────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> All()
    {
        if (!IsAdmin()) return StatusCode(StatusCodes.Status403Forbidden, new { error = "Admin only" });
        var polls = await _service.GetAllAsync();
        return Ok(polls);
    }

    private bool IsAdmin()
        => Request.Headers.TryGetValue("X-User-Role", out var role)
           && role.ToString() == "Admin";
}
