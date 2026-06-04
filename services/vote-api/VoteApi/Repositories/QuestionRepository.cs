using Microsoft.EntityFrameworkCore;
using VoteApi.Data;
using VoteApi.Models;

namespace VoteApi.Repositories;

/// <summary>Data access for Q&A questions. Methods are virtual for unit-test mocking.</summary>
public class QuestionRepository
{
    private readonly VoteDbContext _db;
    public QuestionRepository(VoteDbContext db) => _db = db;

    public virtual async Task AddAsync(Question question)
    {
        _db.Questions.Add(question);
        await _db.SaveChangesAsync();
    }

    public virtual Task<Question?> GetByIdAsync(Guid id)
        => _db.Questions.FirstOrDefaultAsync(q => q.Id == id);

    public virtual async Task UpdateAsync(Question question)
    {
        _db.Questions.Update(question);
        await _db.SaveChangesAsync();
    }

    public virtual async Task DeleteAsync(Question question)
    {
        _db.Questions.Remove(question);
        await _db.SaveChangesAsync();
    }

    // ── Upvote dedup (one per voter key per question) ───────────
    public virtual Task<bool> HasUpvotedAsync(Guid questionId, string voterKey)
        => _db.QuestionUpvotes.AnyAsync(u => u.QuestionId == questionId && u.VoterKey == voterKey);

    public virtual async Task AddUpvoteAsync(Guid questionId, string voterKey)
    {
        _db.QuestionUpvotes.Add(new QuestionUpvote { QuestionId = questionId, VoterKey = voterKey });
        await _db.SaveChangesAsync();
    }

    /// <summary>Pinned first, then most-upvoted, then oldest.</summary>
    public virtual async Task<List<Question>> GetByPollAsync(string pollCode)
        => await _db.Questions
            .Where(q => q.PollCode == pollCode)
            .OrderByDescending(q => q.IsPinned)
            .ThenByDescending(q => q.Upvotes)
            .ThenBy(q => q.CreatedAt)
            .ToListAsync();
}
