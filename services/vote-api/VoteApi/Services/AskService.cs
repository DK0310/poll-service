using Microsoft.AspNetCore.SignalR;
using VoteApi.Common;
using VoteApi.DTOs;
using VoteApi.Hubs;
using VoteApi.Models;
using VoteApi.Repositories;

namespace VoteApi.Services;

/// <summary>
/// Audience Q&A ("Ask") for a poll: anyone can post a question or upvote one; the owner/admin can
/// pin or delete. Any change re-broadcasts the list to the poll's SignalR group ("ReceiveAskUpdate").
/// These are separate from the survey questions, which belong to poll-api.
/// </summary>
public class AskService
{
    private readonly AskRepository _repo;
    private readonly PollClientService _pollClient;
    private readonly IHubContext<PollHub> _hub;

    public AskService(AskRepository repo, PollClientService pollClient, IHubContext<PollHub> hub)
    {
        _repo = repo;
        _pollClient = pollClient;
        _hub = hub;
    }

    public async Task<Result<AskResponse>> SubmitAsync(string code, SubmitAskRequest request)
    {
        var poll = await _pollClient.GetPollAsync(code);
        if (poll is null)
            return Result<AskResponse>.Failure("Poll not found");
        if (string.IsNullOrWhiteSpace(request.Text))
            return Result<AskResponse>.Failure("Question text is required");

        var question = new AudienceQuestion { PollCode = code, Text = request.Text.Trim() };
        await _repo.AddAsync(question);
        await BroadcastAsync(code);
        return Result<AskResponse>.Success(AskResponse.From(question));
    }

    public async Task<Result<List<AskResponse>>> GetForPollAsync(string code)
    {
        var poll = await _pollClient.GetPollAsync(code);
        if (poll is null)
            return Result<List<AskResponse>>.Failure("Poll not found");

        var questions = await _repo.GetByPollAsync(code);
        return Result<List<AskResponse>>.Success(questions.Select(AskResponse.From).ToList());
    }

    // One upvote per person per question. voterKey is the user id when logged in, else a browser token.
    public async Task<Result<AskResponse>> UpvoteAsync(string code, Guid id, string voterKey)
    {
        if (string.IsNullOrWhiteSpace(voterKey))
            return Result<AskResponse>.Failure("A voter token is required");

        var question = await _repo.GetByIdAsync(id);
        if (question is null || question.PollCode != code)
            return Result<AskResponse>.Failure("Question not found");

        if (await _repo.HasUpvotedAsync(id, voterKey))
            return Result<AskResponse>.Failure("You have already upvoted this question");

        await _repo.AddUpvoteAsync(id, voterKey);
        question.Upvotes++;
        await _repo.UpdateAsync(question);
        await BroadcastAsync(code);
        return Result<AskResponse>.Success(AskResponse.From(question));
    }

    // Pin/unpin a question: poll owner or admin only.
    public async Task<Result<AskResponse>> TogglePinAsync(string code, Guid id, Guid? userId, bool isAdmin)
    {
        var question = await _repo.GetByIdAsync(id);
        if (question is null || question.PollCode != code)
            return Result<AskResponse>.Failure("Question not found");

        var gate = await EnsureOwnerOrAdminAsync(code, userId, isAdmin);
        if (!gate.IsSuccess) return Result<AskResponse>.Failure(gate.Error!);

        question.IsPinned = !question.IsPinned;
        await _repo.UpdateAsync(question);
        await BroadcastAsync(code);
        return Result<AskResponse>.Success(AskResponse.From(question));
    }

    // Delete a question: poll owner or admin only.
    public async Task<Result<bool>> DeleteAsync(string code, Guid id, Guid? userId, bool isAdmin)
    {
        var question = await _repo.GetByIdAsync(id);
        if (question is null || question.PollCode != code)
            return Result<bool>.Failure("Question not found");

        var gate = await EnsureOwnerOrAdminAsync(code, userId, isAdmin);
        if (!gate.IsSuccess) return Result<bool>.Failure(gate.Error!);

        await _repo.DeleteAsync(question);
        await BroadcastAsync(code);
        return Result<bool>.Success(true);
    }

    // Ownership check: admins always pass; otherwise the caller must be the poll's creator
    // (CreatorId comes from poll-api).
    private async Task<Result<bool>> EnsureOwnerOrAdminAsync(string code, Guid? userId, bool isAdmin)
    {
        if (isAdmin) return Result<bool>.Success(true);
        var poll = await _pollClient.GetPollAsync(code);
        if (poll is null) return Result<bool>.Failure("Poll not found");
        return userId is not null && poll.CreatorId == userId
            ? Result<bool>.Success(true)
            : Result<bool>.Failure("Forbidden — poll owner or admin only");
    }

    private async Task BroadcastAsync(string code)
    {
        var list = (await _repo.GetByPollAsync(code)).Select(AskResponse.From).ToList();
        await _hub.Clients.Group(code).SendAsync("ReceiveAskUpdate", list);
    }
}
