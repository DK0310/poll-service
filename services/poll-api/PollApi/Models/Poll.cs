namespace PollApi.Models;

public enum PollStatus
{
    Open,
    Closed
}

public class Poll
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";

    /// <summary>Optional survey title. Null = untitled (the UI falls back to the first question).</summary>
    public string? Title { get; set; }
    public PollStatus Status { get; set; } = PollStatus.Open;
    public DateTime? ExpiresAt { get; set; }

    /// <summary>User who created the poll (from the JWT via X-User-Id). NOT a FK — no Users table here.</summary>
    public Guid? CreatorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation (1-to-many): a poll has many questions, each owning its options.
    public ICollection<Question> Questions { get; set; } = new List<Question>();

    // Computed domain state (not persisted)
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
    public bool IsClosed => Status == PollStatus.Closed;
    public bool IsActive => !IsExpired && !IsClosed;
}
