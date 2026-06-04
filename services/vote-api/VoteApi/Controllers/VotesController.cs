using Microsoft.AspNetCore.Mvc;
using VoteApi.DTOs;
using VoteApi.Services;

namespace VoteApi.Controllers;

[ApiController]
[Route("api/polls")]
public class VotesController : ControllerBase
{
    private readonly VoteService _service;
    public VotesController(VoteService service) => _service = service;

    // ── POST /api/polls/{code}/vote ─────────────────────────────
    [HttpPost("{code}/vote")]
    public async Task<IActionResult> Vote(string code, [FromBody] VoteRequest request)
    {
        var result = await _service.SubmitVoteAsync(code, request);
        if (result.IsSuccess)
            return Ok(result.Value);

        if (result.Error!.Contains("already voted", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { error = result.Error });
        if (result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(new { error = result.Error });
        return BadRequest(new { error = result.Error });
    }

    // ── GET /api/polls/{code}/results ───────────────────────────
    [HttpGet("{code}/results")]
    public async Task<IActionResult> Results(string code)
    {
        var result = await _service.GetResultsAsync(code);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { error = result.Error });
    }

    // ── GET /api/polls/{code}/analytics ─────────────────────────
    // Owner-or-admin only (Gateway requires auth; we enforce ownership here).
    [HttpGet("{code}/analytics")]
    public async Task<IActionResult> Analytics(string code)
    {
        var userId = Request.Headers.TryGetValue("X-User-Id", out var v) && Guid.TryParse(v.ToString(), out var id)
            ? (Guid?)id
            : null;
        var isAdmin = Request.Headers.TryGetValue("X-User-Role", out var role) && role.ToString() == "Admin";

        var result = await _service.GetAnalyticsAsync(code, userId, isAdmin);
        if (result.IsSuccess) return Ok(result.Value);
        return result.Error!.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
            ? StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error })
            : NotFound(new { error = result.Error });
    }
}

