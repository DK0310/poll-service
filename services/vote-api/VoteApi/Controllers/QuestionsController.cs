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
    [HttpPost("{code}/questions/{id:guid}/upvote")]
    public async Task<IActionResult> Upvote(string code, Guid id)
    {
        var result = await _service.UpvoteAsync(code, id);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    // ── POST /api/polls/{code}/questions/{id}/pin ───────────────
    [HttpPost("{code}/questions/{id:guid}/pin")]
    public async Task<IActionResult> Pin(string code, Guid id)
    {
        var result = await _service.TogglePinAsync(code, id);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }
}
