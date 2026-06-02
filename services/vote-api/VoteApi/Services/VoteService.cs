using Microsoft.AspNetCore.SignalR;
using VoteApi.Common;
using VoteApi.DTOs;
using VoteApi.Hubs;
using VoteApi.Models;
using VoteApi.Repositories;

namespace VoteApi.Services;

public class VoteService
{
    private readonly VoteRepository _repo;
    private readonly PollClientService _pollClient;
    private readonly IHubContext<PollHub> _hub;

    public VoteService(VoteRepository repo, PollClientService pollClient, IHubContext<PollHub> hub)
    {
        _repo = repo;
        _pollClient = pollClient;
        _hub = hub;
    }

    public async Task<Result<VoteResultsResponse>> SubmitVoteAsync(string code, VoteRequest request)
    {
        // 1. Validate the poll exists and is active (inter-service call to Poll API)
        var poll = await _pollClient.GetPollAsync(code);
        if (poll is null)
            return Result<VoteResultsResponse>.Failure("Poll not found");
        if (!poll.IsActive)
            return Result<VoteResultsResponse>.Failure("Poll is closed or expired");

        // 2. Validate the chosen option
        if (request.OptionIndex < 0 || request.OptionIndex >= poll.Options.Count)
            return Result<VoteResultsResponse>.Failure("Invalid option selected");

        // 3. Validate the voter token
        if (string.IsNullOrWhiteSpace(request.VoterToken))
            return Result<VoteResultsResponse>.Failure("Voter token is required");

        // 4. Enforce one vote per voter per poll
        if (await _repo.HasVotedAsync(code, request.VoterToken))
            return Result<VoteResultsResponse>.Failure("You have already voted on this poll");

        // 5. Save the vote
        await _repo.AddAsync(new Vote
        {
            PollCode = code,
            OptionIndex = request.OptionIndex,
            VoterToken = request.VoterToken,
            VotedAt = DateTime.UtcNow
        });

        // 6. Build updated results
        var results = await BuildResultsAsync(code, poll);

        // 7. Broadcast the new results to everyone watching this poll's group
        await _hub.Clients.Group(code).SendAsync("ReceiveVoteUpdate", results);

        return Result<VoteResultsResponse>.Success(results);
    }

    public async Task<Result<VoteResultsResponse>> GetResultsAsync(string code)
    {
        var poll = await _pollClient.GetPollAsync(code);
        if (poll is null)
            return Result<VoteResultsResponse>.Failure("Poll not found");

        var results = await BuildResultsAsync(code, poll);
        return Result<VoteResultsResponse>.Success(results);
    }

    private async Task<VoteResultsResponse> BuildResultsAsync(string code, PollInfo poll)
    {
        var voteCounts = await _repo.GetVoteCountsAsync(code);
        var totalVotes = voteCounts.Sum(vc => vc.Count);

        return new VoteResultsResponse
        {
            PollCode = code,
            Question = poll.Question,
            TotalVotes = totalVotes,
            IsActive = poll.IsActive,
            Options = poll.Options
                .OrderBy(o => o.OptionIndex)
                .Select(o =>
                {
                    var count = voteCounts.FirstOrDefault(vc => vc.OptionIndex == o.OptionIndex)?.Count ?? 0;
                    return new OptionResult
                    {
                        OptionIndex = o.OptionIndex,
                        Text = o.Text,
                        VoteCount = count,
                        Percentage = totalVotes > 0 ? Math.Round((double)count / totalVotes * 100, 1) : 0
                    };
                })
                .ToList()
        };
    }
}
