using Microsoft.AspNetCore.Mvc;
using VoteApi.DTOs;
using VoteApi.Services;

namespace VoteApi.Controllers;

[ApiController]
[Route("api/polls")]
public class QuestionsController : ControllerBase
{
    private readonly QuestionService _service;
    public QuestionsController(QuestionService service) => _service = service;

    // ── GET /api/polls/{code}/questions ─────────────────────────
    [HttpGet("{code}/questions")]
    public async Task<IActionResult> List(string code)
    {
        var result = await _service.GetForPollAsync(code);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    // ── POST /api/polls/{code}/questions ────────────────────────
    [HttpPost("{code}/questions")]
    public async Task<IActionResult> Submit(string code, [FromBody] SubmitQuestionRequest request)
    {
        var result = await _service.SubmitAsync(code, request);
        if (result.IsSuccess) return Ok(result.Value);
        return result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? NotFound(new { error = result.Error })
            : BadRequest(new { error = result.Error });
    }

    // ── POST /api/polls/{code}/questions/{id}/upvote ────────────
    // One upvote per person: logged-in users dedup by X-User-Id; guests by their voter token.
    [HttpPost("{code}/questions/{id:guid}/upvote")]
    public async Task<IActionResult> Upvote(string code, Guid id, [FromBody] UpvoteRequest? request)
    {
        var voterKey = UserId()?.ToString() ?? request?.VoterToken?.Trim();
        if (string.IsNullOrWhiteSpace(voterKey))
            return BadRequest(new { error = "A voter token is required" });

        var result = await _service.UpvoteAsync(code, id, voterKey);
        if (result.IsSuccess) return Ok(result.Value);
        if (result.Error!.Contains("already upvoted", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { error = result.Error });
        return NotFound(new { error = result.Error });
    }

    // ── POST /api/polls/{code}/questions/{id}/pin ───────────────
    [HttpPost("{code}/questions/{id:guid}/pin")]
    public async Task<IActionResult> Pin(string code, Guid id)
    {
        var result = await _service.TogglePinAsync(code, id, UserId(), IsAdmin());
        return Map(result.IsSuccess, result.Error, () => Ok(result.Value));
    }

    // ── DELETE /api/polls/{code}/questions/{id} ─────────────────
    [HttpDelete("{code}/questions/{id:guid}")]
    public async Task<IActionResult> Delete(string code, Guid id)
    {
        var result = await _service.DeleteAsync(code, id, UserId(), IsAdmin());
        return Map(result.IsSuccess, result.Error, NoContent);
    }

    private IActionResult Map(bool ok, string? error, Func<IActionResult> onOk)
    {
        if (ok) return onOk();
        if (error!.Contains("forbidden", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden, new { error });
        return NotFound(new { error });
    }

    private Guid? UserId()
        => Request.Headers.TryGetValue("X-User-Id", out var v) && Guid.TryParse(v.ToString(), out var id)
            ? id
            : null;

    private bool IsAdmin()
        => Request.Headers.TryGetValue("X-User-Role", out var role) && role.ToString() == "Admin";
}
