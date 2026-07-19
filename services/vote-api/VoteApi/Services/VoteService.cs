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

    /// <summary>
    /// Records one survey submission (all questions answered at once). Everything is validated
    /// up front so a partial or invalid batch never touches the database.
    /// </summary>
    public async Task<Result<VoteResultsResponse>> SubmitVoteAsync(string code, VoteRequest request, Guid? userId = null)
    {
        // The poll lives in poll-api, so we call across to confirm it exists and still accepts votes.
        var poll = await _pollClient.GetPollAsync(code);
        if (poll is null)
            return Result<VoteResultsResponse>.Failure("Poll not found");
        if (!poll.IsActive)
            return Result<VoteResultsResponse>.Failure("Poll is closed or expired");

        if (string.IsNullOrWhiteSpace(request.VoterToken))
            return Result<VoteResultsResponse>.Failure("Voter token is required");

        // NOTE: one-vote-per-poll is keyed on VoterToken, which the browser generates and stores
        // locally. It stops casual double-voting but isn't tamper-proof (clearing storage or using
        // incognito gets a new token). For real anti-fraud, gate on the authenticated userId instead.
        if (await _repo.HasVotedAsync(code, request.VoterToken))
            return Result<VoteResultsResponse>.Failure("You have already voted on this poll");

        if (request.Answers is null || request.Answers.Count == 0)
            return Result<VoteResultsResponse>.Failure("An answer for every question is required");

        // Validate the whole batch against the poll's real questions/options before saving anything.
        var questionsById = poll.Questions.ToDictionary(q => q.Id);
        var answered = new HashSet<Guid>();
        var votes = new List<Vote>();

        foreach (var a in request.Answers)
        {
            if (!questionsById.TryGetValue(a.QuestionId, out var q))
                return Result<VoteResultsResponse>.Failure("Answer references an unknown question");
            if (!answered.Add(a.QuestionId))
                return Result<VoteResultsResponse>.Failure("Duplicate answer for a question");

            var isOpenText = string.Equals(q.Type, "OpenText", StringComparison.OrdinalIgnoreCase);
            string? textAnswer = null;
            string? authorName = null;
            string? authorRole = null;
            var optionIndex = a.OptionIndex;

            if (isOpenText)
            {
                if (string.IsNullOrWhiteSpace(a.TextAnswer))
                    return Result<VoteResultsResponse>.Failure("A text answer is required");
                textAnswer = a.TextAnswer.Trim();
                // Author label is display-only and comes from the client (null = anonymous guest).
                authorName = string.IsNullOrWhiteSpace(request.AuthorName) ? null : request.AuthorName.Trim();
                authorRole = string.IsNullOrWhiteSpace(request.AuthorRole) ? null : request.AuthorRole.Trim();
                optionIndex = 0; // not used for tallying
            }
            else if (optionIndex < 0 || optionIndex >= q.Options.Count)
            {
                return Result<VoteResultsResponse>.Failure("Invalid option selected");
            }

            votes.Add(new Vote
            {
                PollCode = code,
                QuestionId = q.Id,
                OptionIndex = optionIndex,
                VoterToken = request.VoterToken,
                UserId = userId,
                TextAnswer = textAnswer,
                AuthorName = authorName,
                AuthorRole = authorRole,
                VotedAt = DateTime.UtcNow
            });
        }

        // Every question must be answered exactly once.
        if (answered.Count != poll.Questions.Count)
            return Result<VoteResultsResponse>.Failure("Please answer every question");

        await _repo.AddRangeAsync(votes);

        // Push the fresh results to everyone watching this poll live (SignalR group = poll code).
        var results = await BuildResultsAsync(code, poll);
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

    /// <summary>Creator analytics: submissions over time (per-minute), peak minute, and each question's
    /// leading option. Restricted to the poll owner or an admin.</summary>
    public async Task<Result<AnalyticsResponse>> GetAnalyticsAsync(string code, Guid? userId, bool isAdmin)
    {
        var poll = await _pollClient.GetPollAsync(code);
        if (poll is null)
            return Result<AnalyticsResponse>.Failure("Poll not found");

        // NOTE: the gateway only guarantees the caller is logged in. Ownership is enforced HERE:
        // unless they're an admin, the caller's id must match the poll's creator.
        if (!isAdmin && (userId is null || poll.CreatorId != userId))
            return Result<AnalyticsResponse>.Failure("Forbidden — poll owner or admin only");

        var counts = await _repo.GetVoteCountsAsync(code);
        var submissions = await _repo.GetSubmissionTimestampsAsync(code);

        // Bucket submissions per minute (UTC) to build the timeline, then pick the busiest bucket.
        var timeline = submissions
            .GroupBy(t => new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, DateTimeKind.Utc))
            .Select(g => new TimeBucket { Minute = g.Key, Count = g.Count() })
            .OrderBy(b => b.Minute)
            .ToList();
        var peak = timeline.Count > 0
            ? timeline.OrderByDescending(b => b.Count).ThenBy(b => b.Minute).First()
            : null;

        var questions = poll.Questions
            .OrderBy(q => q.QuestionIndex)
            .Select(q =>
            {
                var isOpenText = string.Equals(q.Type, "OpenText", StringComparison.OrdinalIgnoreCase);
                var qCounts = counts.Where(c => c.QuestionId == q.Id).ToList();
                var totalVotes = qCounts.Sum(c => c.Count);

                TopOption? top = null;
                if (!isOpenText && qCounts.Count > 0)
                {
                    var best = qCounts.OrderByDescending(c => c.Count).First();
                    var text = q.Options.FirstOrDefault(o => o.OptionIndex == best.OptionIndex)?.Text
                               ?? $"Option {best.OptionIndex}";
                    top = new TopOption { OptionIndex = best.OptionIndex, Text = text, VoteCount = best.Count };
                }

                return new QuestionAnalytics
                {
                    QuestionId = q.Id,
                    QuestionIndex = q.QuestionIndex,
                    Text = q.Text,
                    Type = q.Type,
                    TotalVotes = totalVotes,
                    TopOption = top
                };
            })
            .ToList();

        return Result<AnalyticsResponse>.Success(new AnalyticsResponse
        {
            PollCode = code,
            Title = poll.Title,
            TotalVoters = submissions.Count,
            Timeline = timeline,
            PeakMinute = peak,
            Questions = questions
        });
    }

    /// <summary>The polls a user has voted on, enriched with each poll's title/state from the Poll API
    /// (the Vote DB only stores the code). Polls that no longer exist are dropped.</summary>
    public async Task<List<VoteHistoryItem>> GetVoteHistoryAsync(Guid userId)
    {
        var voted = await _repo.GetVotedPollsAsync(userId);

        var items = new List<VoteHistoryItem>();
        foreach (var v in voted)
        {
            var poll = await _pollClient.GetPollAsync(v.PollCode);
            if (poll is null) continue; // deleted poll — skip
            items.Add(new VoteHistoryItem
            {
                PollCode = v.PollCode,
                Title = poll.Title,
                IsActive = poll.IsActive,
                AnswerCount = v.AnswerCount,
                VotedAt = v.VotedAt
            });
        }
        return items;
    }

    private async Task<VoteResultsResponse> BuildResultsAsync(string code, PollInfo poll)
    {
        var counts = await _repo.GetVoteCountsAsync(code);
        var texts = await _repo.GetTextAnswersAsync(code);
        var totalVoters = await _repo.GetVoterCountAsync(code);

        var questions = poll.Questions
            .OrderBy(q => q.QuestionIndex)
            .Select(q =>
            {
                // OpenText questions aren't tallied — return the submitted text answers instead.
                if (string.Equals(q.Type, "OpenText", StringComparison.OrdinalIgnoreCase))
                {
                    var answers = texts.Where(t => t.QuestionId == q.Id).Select(t => t.Answer).ToList();
                    return new QuestionResults
                    {
                        QuestionId = q.Id,
                        QuestionIndex = q.QuestionIndex,
                        Text = q.Text,
                        Type = q.Type,
                        TotalVotes = answers.Count,
                        TextAnswers = answers
                    };
                }

                var qCounts = counts.Where(c => c.QuestionId == q.Id).ToList();
                var totalVotes = qCounts.Sum(c => c.Count);
                return new QuestionResults
                {
                    QuestionId = q.Id,
                    QuestionIndex = q.QuestionIndex,
                    Text = q.Text,
                    Type = q.Type,
                    TotalVotes = totalVotes,
                    Options = q.Options
                        .OrderBy(o => o.OptionIndex)
                        .Select(o =>
                        {
                            var count = qCounts.FirstOrDefault(c => c.OptionIndex == o.OptionIndex)?.Count ?? 0;
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
            })
            .ToList();

        return new VoteResultsResponse
        {
            PollCode = code,
            Title = poll.Title,
            IsActive = poll.IsActive,
            TotalVoters = totalVoters,
            Questions = questions
        };
    }
}
