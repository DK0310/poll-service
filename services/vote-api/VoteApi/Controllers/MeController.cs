using Microsoft.AspNetCore.Mvc;
using VoteApi.Services;

namespace VoteApi.Controllers;

// Per-account voter data. Auth is enforced at the gateway; we read the caller from X-User-Id.
[ApiController]
[Route("api/me")]
public class MeController : ControllerBase
{
    private readonly VoteService _service;
    public MeController(VoteService service) => _service = service;

    [HttpGet("votes")]
    public async Task<IActionResult> Votes()
    {
        var userId = Request.Headers.TryGetValue("X-User-Id", out var v) && Guid.TryParse(v.ToString(), out var id)
            ? (Guid?)id
            : null;
        if (userId is null) return Unauthorized(new { error = "Not authenticated" });

        return Ok(await _service.GetVoteHistoryAsync(userId.Value));
    }
}
