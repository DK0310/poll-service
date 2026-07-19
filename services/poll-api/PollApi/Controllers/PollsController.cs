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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePollRequest request)
    {
        // Creating a poll needs a logged-in user. The gateway gates this route and sets X-User-Id,
        // so no header means the request didn't come through authenticated.
        if (!TryGetUserId(out var creatorId)) return Unauthorized();

        var result = await _service.CreateAsync(request, creatorId);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetPoll), new { code = result.Value!.Code }, result.Value)
            : BadRequest(new { error = result.Error });
    }

    // The literal "my-polls" segment is matched ahead of {code}, and the gateway only routes here
    // with a valid token.
    [HttpGet("my-polls")]
    public async Task<IActionResult> MyPolls()
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var polls = await _service.GetByCreatorAsync(userId);
        return Ok(polls);
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> GetPoll(string code)
    {
        var result = await _service.GetByCodeAsync(code);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { error = result.Error });
    }

    [HttpPatch("{code}/close")]
    public async Task<IActionResult> Close(string code)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _service.CloseAsync(code, userId, IsAdmin());
        if (result.IsSuccess) return Ok(result.Value);
        return MapFailure(result.Error!);
    }

    [HttpDelete("{code}")]
    public async Task<IActionResult> Delete(string code)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _service.DeleteAsync(code, userId, IsAdmin());
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

    /// <summary>True when the Gateway forwarded X-User-Role: Admin (admins bypass ownership).</summary>
    private bool IsAdmin()
        => Request.Headers.TryGetValue("X-User-Role", out var role)
           && role.ToString() == "Admin";

    // NOTE: poll-api registers no auth scheme (the gateway does all JWT work), so Forbid()/Challenge()
    // would throw for lack of a scheme. We return the status codes explicitly instead.
    private IActionResult MapFailure(string error)
    {
        if (error.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(new { error });
        if (error.Contains("not the poll creator", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden, new { error });
        return BadRequest(new { error });
    }
}
