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

        // 2. Validate the voter token
        if (string.IsNullOrWhiteSpace(request.VoterToken))
            return Result<VoteResultsResponse>.Failure("Voter token is required");

        // 3. Validate the answer per poll type (OpenText = free text; others = option index)
        var isOpenText = string.Equals(poll.Type, "OpenText", StringComparison.OrdinalIgnoreCase);
        string? textAnswer = null;
        var optionIndex = request.OptionIndex;
        if (isOpenText)
        {
            if (string.IsNullOrWhiteSpace(request.TextAnswer))
                return Result<VoteResultsResponse>.Failure("A text answer is required");
            textAnswer = request.TextAnswer.Trim();
            optionIndex = 0; // not used for tallying
        }
        else if (optionIndex < 0 || optionIndex >= poll.Options.Count)
        {
            return Result<VoteResultsResponse>.Failure("Invalid option selected");
        }

        // 4. Enforce one vote per voter per poll
        if (await _repo.HasVotedAsync(code, request.VoterToken))
            return Result<VoteResultsResponse>.Failure("You have already voted on this poll");

        // 5. Save the vote
        await _repo.AddAsync(new Vote
        {
            PollCode = code,
            OptionIndex = optionIndex,
            VoterToken = request.VoterToken,
            TextAnswer = textAnswer,
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

    /// <summary>Creator analytics: votes over time (per-minute), peak minute, and the leading option.
    /// Restricted to the poll owner or an admin.</summary>
    public async Task<Result<AnalyticsResponse>> GetAnalyticsAsync(string code, Guid? userId, bool isAdmin)
    {
        var poll = await _pollClient.GetPollAsync(code);
        if (poll is null)
            return Result<AnalyticsResponse>.Failure("Poll not found");

        // Owner-or-admin gate.
        if (!isAdmin && (userId is null || poll.CreatorId != userId))
            return Result<AnalyticsResponse>.Failure("Forbidden — poll owner or admin only");

        var counts = await _repo.GetVoteCountsAsync(code);
        var timestamps = await _repo.GetVoteTimestampsAsync(code);
        var totalVotes = counts.Sum(c => c.Count);

        // Leading option (null when there are no votes yet, or for OpenText polls)
        var isOpenText = string.Equals(poll.Type, "OpenText", StringComparison.OrdinalIgnoreCase);
        TopOption? topOption = null;
        if (!isOpenText && counts.Count > 0)
        {
            var best = counts.OrderByDescending(c => c.Count).First();
            var text = poll.Options.FirstOrDefault(o => o.OptionIndex == best.OptionIndex)?.Text
                       ?? $"Option {best.OptionIndex}";
            topOption = new TopOption { OptionIndex = best.OptionIndex, Text = text, VoteCount = best.Count };
        }

        // Per-minute buckets (truncate VotedAt to the minute, UTC) → timeline + peak
        var timeline = timestamps
            .GroupBy(t => new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, DateTimeKind.Utc))
            .Select(g => new TimeBucket { Minute = g.Key, Count = g.Count() })
            .OrderBy(b => b.Minute)
            .ToList();
        var peak = timeline.Count > 0
            ? timeline.OrderByDescending(b => b.Count).ThenBy(b => b.Minute).First()
            : null;

        return Result<AnalyticsResponse>.Success(new AnalyticsResponse
        {
            PollCode = code,
            Question = poll.Question,
            TotalVotes = totalVotes,
            TopOption = topOption,
            PeakMinute = peak,
            Timeline = timeline
        });
    }

    private async Task<VoteResultsResponse> BuildResultsAsync(string code, PollInfo poll)
    {
        // OpenText polls aren't tallied — return the submitted text answers instead of option counts.
        if (string.Equals(poll.Type, "OpenText", StringComparison.OrdinalIgnoreCase))
        {
            var answers = await _repo.GetTextAnswersAsync(code);
            return new VoteResultsResponse
            {
                PollCode = code,
                Question = poll.Question,
                Type = poll.Type,
                TotalVotes = answers.Count,
                IsActive = poll.IsActive,
                TextAnswers = answers
            };
        }

        var voteCounts = await _repo.GetVoteCountsAsync(code);
        var totalVotes = voteCounts.Sum(vc => vc.Count);

        return new VoteResultsResponse
        {
            PollCode = code,
            Question = poll.Question,
            Type = poll.Type,
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
