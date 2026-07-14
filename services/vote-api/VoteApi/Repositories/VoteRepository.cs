using Microsoft.EntityFrameworkCore;
using VoteApi.Data;
using VoteApi.DTOs;
using VoteApi.Models;

namespace VoteApi.Repositories;

/// <summary>
/// Data access for votes. Methods are virtual so the service layer can be
/// unit-tested with a mocked repository (Moq).
/// </summary>
public class VoteRepository
{
    private readonly VoteDbContext _db;
    public VoteRepository(VoteDbContext db) => _db = db;

    public virtual async Task AddRangeAsync(IEnumerable<Vote> votes)
    {
        _db.Votes.AddRange(votes);
        await _db.SaveChangesAsync();
    }

    /// <summary>True once the voter has submitted any answer for the poll (one submission per voter per poll).</summary>
    public virtual Task<bool> HasVotedAsync(string pollCode, string voterToken)
        => _db.Votes.AnyAsync(v => v.PollCode == pollCode && v.VoterToken == voterToken);

    /// <summary>Distinct voters who submitted the survey.</summary>
    public virtual Task<int> GetVoterCountAsync(string pollCode)
        => _db.Votes.Where(v => v.PollCode == pollCode).Select(v => v.VoterToken).Distinct().CountAsync();

    /// <summary>Aggregates vote counts per (question, option) in SQL (GROUP BY), not in memory.</summary>
    public virtual async Task<List<VoteCount>> GetVoteCountsAsync(string pollCode)
        => await _db.Votes
            .Where(v => v.PollCode == pollCode)
            .GroupBy(v => new { v.QuestionId, v.OptionIndex })
            .Select(g => new VoteCount { QuestionId = g.Key.QuestionId, OptionIndex = g.Key.OptionIndex, Count = g.Count() })
            .ToListAsync();

    /// <summary>Each voter's submission time (their earliest vote), for the submissions-over-time chart.</summary>
    public virtual async Task<List<DateTime>> GetSubmissionTimestampsAsync(string pollCode)
        => await _db.Votes
            .Where(v => v.PollCode == pollCode)
            .GroupBy(v => v.VoterToken)
            .Select(g => g.Min(v => v.VotedAt))
            .ToListAsync();

    /// <summary>Distinct polls a logged-in user has voted on, most-recent submission first (capped).</summary>
    public virtual async Task<List<VotedPoll>> GetVotedPollsAsync(Guid userId, int limit = 50)
        => await _db.Votes
            .Where(v => v.UserId == userId)
            .GroupBy(v => v.PollCode)
            .Select(g => new VotedPoll
            {
                PollCode = g.Key,
                AnswerCount = g.Count(),
                VotedAt = g.Max(v => v.VotedAt)
            })
            .OrderByDescending(p => p.VotedAt)
            .Take(limit)
            .ToListAsync();

    /// <summary>Free-text answers (with author info + owning question) for the poll, oldest first.</summary>
    public virtual async Task<List<QuestionTextAnswer>> GetTextAnswersAsync(string pollCode)
        => await _db.Votes
            .Where(v => v.PollCode == pollCode && v.TextAnswer != null)
            .OrderBy(v => v.VotedAt)
            .Select(v => new QuestionTextAnswer
            {
                QuestionId = v.QuestionId,
                Answer = new TextAnswerResponse
                {
                    Text = v.TextAnswer!,
                    AuthorName = v.AuthorName,
                    AuthorRole = v.AuthorRole,
                    VotedAt = v.VotedAt
                }
            })
            .ToListAsync();
}

public class VoteCount
{
    public Guid QuestionId { get; set; }
    public int OptionIndex { get; set; }
    public int Count { get; set; }
}

public class QuestionTextAnswer
{
    public Guid QuestionId { get; set; }
    public TextAnswerResponse Answer { get; set; } = new();
}

public class VotedPoll
{
    public string PollCode { get; set; } = "";
    public int AnswerCount { get; set; }
    public DateTime VotedAt { get; set; }
}
