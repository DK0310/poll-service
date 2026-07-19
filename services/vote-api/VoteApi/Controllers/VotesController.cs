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

    [HttpPost("{code}/vote")]
    public async Task<IActionResult> Vote(string code, [FromBody] VoteRequest request)
    {
        // Voting is public, but if the caller happened to be logged in the gateway forwards their
        // X-User-Id. We capture it so the vote shows up in that user's history (optional, may be null).
        var userId = Request.Headers.TryGetValue("X-User-Id", out var v) && Guid.TryParse(v.ToString(), out var id)
            ? (Guid?)id
            : null;
        var result = await _service.SubmitVoteAsync(code, request, userId);
        if (result.IsSuccess)
            return Ok(result.Value);

        if (result.Error!.Contains("already voted", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { error = result.Error });
        if (result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(new { error = result.Error });
        return BadRequest(new { error = result.Error });
    }

    [HttpGet("{code}/results")]
    public async Task<IActionResult> Results(string code)
    {
        var result = await _service.GetResultsAsync(code);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { error = result.Error });
    }

    // Gateway guarantees the caller is authenticated; the owner-or-admin check lives in the service.
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

