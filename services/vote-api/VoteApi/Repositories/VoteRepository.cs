using Microsoft.EntityFrameworkCore;
using VoteApi.Data;
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
}

public class VoteCount
{
    public int OptionIndex { get; set; }
    public int Count { get; set; }
}
