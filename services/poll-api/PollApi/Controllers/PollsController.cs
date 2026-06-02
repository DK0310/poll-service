using Microsoft.AspNetCore.Mvc;
using PollApi.DTOs;
using PollApi.Services;

namespace PollApi.Controllers;

[ApiController]
[Route("api/polls")]
public class PollsController : ControllerBase
{
    private readonly PollService _service;
    public PollsController(PollService service) => _service = service;

    // ── POST /api/polls ─────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePollRequest request)
    {
        // CreatorId comes from the X-User-Id header the Gateway sets after JWT validation.
        // Creation is anonymous-friendly: no header → no creator.
        Guid? creatorId = Request.Headers.TryGetValue("X-User-Id", out var uid)
            && Guid.TryParse(uid.ToString(), out var id)
            ? id
            : null;

        var result = await _service.CreateAsync(request, creatorId);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetPoll), new { code = result.Value!.Code }, result.Value)
            : BadRequest(new { error = result.Error });
    }

    // ── GET /api/polls/my-polls ─────────────────────────────────
    // Literal segment wins over {code}; the Gateway only routes here with a valid JWT.
    [HttpGet("my-polls")]
    public async Task<IActionResult> MyPolls()
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var polls = await _service.GetByCreatorAsync(userId);
        return Ok(polls);
    }

    // ── GET /api/polls/{code} ───────────────────────────────────
    [HttpGet("{code}")]
    public async Task<IActionResult> GetPoll(string code)
    {
        var result = await _service.GetByCodeAsync(code);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { error = result.Error });
    }

    // ── PATCH /api/polls/{code}/close ───────────────────────────
    [HttpPatch("{code}/close")]
    public async Task<IActionResult> Close(string code)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _service.CloseAsync(code, userId);
        if (result.IsSuccess) return Ok(result.Value);
        return MapFailure(result.Error!);
    }

    // ── DELETE /api/polls/{code} ────────────────────────────────
    [HttpDelete("{code}")]
    public async Task<IActionResult> Delete(string code)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _service.DeleteAsync(code, userId);
        if (result.IsSuccess) return NoContent();
        return MapFailure(result.Error!);
    }

    /// <summary>Reads the user id the Gateway forwards in X-User-Id after validating the JWT.</summary>
    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        return Request.Headers.TryGetValue("X-User-Id", out var value)
            && Guid.TryParse(value.ToString(), out userId);
    }

    // poll-api has no auth scheme registered (the Gateway validates JWTs), so we return
    // status codes directly rather than Forbid()/Challenge() which require a scheme.
    private IActionResult MapFailure(string error)
    {
        if (error.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(new { error });
        if (error.Contains("not the poll creator", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden, new { error });
        return BadRequest(new { error });
    }
}
