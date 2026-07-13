using Microsoft.EntityFrameworkCore;
using PollApi.Data;
using PollApi.Models;

namespace PollApi.Repositories;

/// <summary>
/// Data access for polls. Methods are virtual so the service layer can be
/// unit-tested with a mocked repository (Moq).
/// </summary>
public class PollRepository
{
    private readonly PollDbContext _db;
    public PollRepository(PollDbContext db) => _db = db;

    public virtual Task<Poll?> GetByCodeAsync(string code)
        => _db.Polls
            .Include(p => p.Questions.OrderBy(q => q.QuestionIndex))
                .ThenInclude(q => q.Options.OrderBy(o => o.OptionIndex))
            .FirstOrDefaultAsync(p => p.Code == code);

    public virtual async Task AddAsync(Poll poll)
    {
        _db.Polls.Add(poll);
        await _db.SaveChangesAsync();
    }

    public virtual async Task UpdateAsync(Poll poll)
    {
        _db.Polls.Update(poll);
        await _db.SaveChangesAsync();
    }

    public virtual async Task DeleteAsync(Poll poll)
    {
        _db.Polls.Remove(poll);
        await _db.SaveChangesAsync();
    }

    public virtual async Task<IEnumerable<Poll>> GetByCreatorAsync(Guid userId, int limit = 50)
        => await _db.Polls
            .Include(p => p.Questions.OrderBy(q => q.QuestionIndex))
                .ThenInclude(q => q.Options.OrderBy(o => o.OptionIndex))
            .Where(p => p.CreatorId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync();

    public virtual async Task<IEnumerable<Poll>> GetAllAsync(int limit = 200)
        => await _db.Polls
            .Include(p => p.Questions.OrderBy(q => q.QuestionIndex))
                .ThenInclude(q => q.Options.OrderBy(o => o.OptionIndex))
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync();

    public virtual async Task<IEnumerable<Poll>> GetExpiredAsync()
        => await _db.Polls
            .Where(p => p.Status == PollStatus.Open
                     && p.ExpiresAt != null
                     && p.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();
}
