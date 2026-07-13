using Microsoft.EntityFrameworkCore;
using VoteApi.Data;
using VoteApi.Models;

namespace VoteApi.Repositories;

/// <summary>Data access for audience "Ask" questions. Methods are virtual for unit-test mocking.</summary>
public class AskRepository
{
    private readonly VoteDbContext _db;
    public AskRepository(VoteDbContext db) => _db = db;

    public virtual async Task AddAsync(AudienceQuestion question)
    {
        _db.AudienceQuestions.Add(question);
        await _db.SaveChangesAsync();
    }

    public virtual Task<AudienceQuestion?> GetByIdAsync(Guid id)
        => _db.AudienceQuestions.FirstOrDefaultAsync(q => q.Id == id);

    public virtual async Task UpdateAsync(AudienceQuestion question)
    {
        _db.AudienceQuestions.Update(question);
        await _db.SaveChangesAsync();
    }

    public virtual async Task DeleteAsync(AudienceQuestion question)
    {
        _db.AudienceQuestions.Remove(question);
        await _db.SaveChangesAsync();
    }

    // ── Upvote dedup (one per voter key per question) ───────────
    public virtual Task<bool> HasUpvotedAsync(Guid audienceQuestionId, string voterKey)
        => _db.AudienceQuestionUpvotes.AnyAsync(u => u.AudienceQuestionId == audienceQuestionId && u.VoterKey == voterKey);

    public virtual async Task AddUpvoteAsync(Guid audienceQuestionId, string voterKey)
    {
        _db.AudienceQuestionUpvotes.Add(new AudienceQuestionUpvote { AudienceQuestionId = audienceQuestionId, VoterKey = voterKey });
        await _db.SaveChangesAsync();
    }

    /// <summary>Pinned first, then most-upvoted, then oldest.</summary>
    public virtual async Task<List<AudienceQuestion>> GetByPollAsync(string pollCode)
        => await _db.AudienceQuestions
            .Where(q => q.PollCode == pollCode)
            .OrderByDescending(q => q.IsPinned)
            .ThenByDescending(q => q.Upvotes)
            .ThenBy(q => q.CreatedAt)
            .ToListAsync();
}
