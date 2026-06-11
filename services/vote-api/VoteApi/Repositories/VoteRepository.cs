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

    public virtual async Task AddAsync(Vote vote)
    {
        _db.Votes.Add(vote);
        await _db.SaveChangesAsync();
    }

    public virtual Task<bool> HasVotedAsync(string pollCode, string voterToken)
        => _db.Votes.AnyAsync(v => v.PollCode == pollCode && v.VoterToken == voterToken);

    /// <summary>Aggregates vote counts per option in SQL (GROUP BY), not in memory.</summary>
    public virtual async Task<List<VoteCount>> GetVoteCountsAsync(string pollCode)
        => await _db.Votes
            .Where(v => v.PollCode == pollCode)
            .GroupBy(v => v.OptionIndex)
            .Select(g => new VoteCount { OptionIndex = g.Key, Count = g.Count() })
            .ToListAsync();

    /// <summary>Ordered vote timestamps for analytics (votes-over-time / peak minute). Uses the VotedAt index.</summary>
    public virtual async Task<List<DateTime>> GetVoteTimestampsAsync(string pollCode)
        => await _db.Votes
            .Where(v => v.PollCode == pollCode)
            .OrderBy(v => v.VotedAt)
            .Select(v => v.VotedAt)
            .ToListAsync();

    /// <summary>Free-text answers for an OpenText poll, oldest first (with author info for the comment feed).</summary>
    public virtual async Task<List<TextAnswerResponse>> GetTextAnswersAsync(string pollCode)
        => await _db.Votes
            .Where(v => v.PollCode == pollCode && v.TextAnswer != null)
            .OrderBy(v => v.VotedAt)
            .Select(v => new TextAnswerResponse
            {
                Text = v.TextAnswer!,
                AuthorName = v.AuthorName,
                AuthorRole = v.AuthorRole,
                VotedAt = v.VotedAt
            })
            .ToListAsync();
}

public class VoteCount
{
    public int OptionIndex { get; set; }
    public int Count { get; set; }
}
