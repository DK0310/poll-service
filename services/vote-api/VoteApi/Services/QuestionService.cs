using Microsoft.AspNetCore.SignalR;
using VoteApi.Common;
using VoteApi.DTOs;
using VoteApi.Hubs;
using VoteApi.Models;
using VoteApi.Repositories;

namespace VoteApi.Services;

/// <summary>
/// Anonymous Q&A for a poll: anyone may submit a question or upvote it; pinning highlights it.
/// Every change broadcasts the refreshed list to the poll's SignalR group ("ReceiveQuestionsUpdate").
/// </summary>
public class QuestionService
{
    private readonly QuestionRepository _repo;
    private readonly PollClientService _pollClient;
    private readonly IHubContext<PollHub> _hub;

    public QuestionService(QuestionRepository repo, PollClientService pollClient, IHubContext<PollHub> hub)
    {
        _repo = repo;
        _pollClient = pollClient;
        _hub = hub;
    }

    public async Task<Result<QuestionResponse>> SubmitAsync(string code, SubmitQuestionRequest request)
    {
        var poll = await _pollClient.GetPollAsync(code);
        if (poll is null)
            return Result<QuestionResponse>.Failure("Poll not found");
        if (string.IsNullOrWhiteSpace(request.Text))
            return Result<QuestionResponse>.Failure("Question text is required");

        var question = new Question { PollCode = code, Text = request.Text.Trim() };
        await _repo.AddAsync(question);
        await BroadcastAsync(code);
        return Result<QuestionResponse>.Success(QuestionResponse.From(question));
    }

    public async Task<Result<List<QuestionResponse>>> GetForPollAsync(string code)
    {
        var poll = await _pollClient.GetPollAsync(code);
        if (poll is null)
            return Result<List<QuestionResponse>>.Failure("Poll not found");

        var questions = await _repo.GetByPollAsync(code);
        return Result<List<QuestionResponse>>.Success(questions.Select(QuestionResponse.From).ToList());
    }

    public async Task<Result<QuestionResponse>> UpvoteAsync(string code, Guid id)
    {
        var question = await _repo.GetByIdAsync(id);
        if (question is null || question.PollCode != code)
            return Result<QuestionResponse>.Failure("Question not found");

        question.Upvotes++;
        await _repo.UpdateAsync(question);
        await BroadcastAsync(code);
        return Result<QuestionResponse>.Success(QuestionResponse.From(question));
    }

    public async Task<Result<QuestionResponse>> TogglePinAsync(string code, Guid id)
    {
        var question = await _repo.GetByIdAsync(id);
        if (question is null || question.PollCode != code)
            return Result<QuestionResponse>.Failure("Question not found");

        question.IsPinned = !question.IsPinned;
        await _repo.UpdateAsync(question);
        await BroadcastAsync(code);
        return Result<QuestionResponse>.Success(QuestionResponse.From(question));
    }

    private async Task BroadcastAsync(string code)
    {
        var list = (await _repo.GetByPollAsync(code)).Select(QuestionResponse.From).ToList();
        await _hub.Clients.Group(code).SendAsync("ReceiveQuestionsUpdate", list);
    }
}
