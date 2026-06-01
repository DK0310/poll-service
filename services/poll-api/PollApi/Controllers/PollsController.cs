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

    // ── GET /api/polls/{code} ───────────────────────────────────
    [HttpGet("{code}")]
    public async Task<IActionResult> GetPoll(string code)
    {
        var result = await _service.GetByCodeAsync(code);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { error = result.Error });
    }

    // close / delete / my-polls are added in Phase 6 (require auth + X-User-Id enforcement)
}
