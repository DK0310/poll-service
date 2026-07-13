namespace VoteApi.Models;

/// <summary>
/// An audience question for a poll (anonymous Q&A / "Ask"). Owned by the Vote API alongside votes,
/// since both are real-time audience interactions keyed by poll code. Distinct from a survey
/// <c>Question</c> (owned by the Poll API) — this is the audience asking the host.
/// </summary>
public class AudienceQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PollCode { get; set; } = "";
    public string Text { get; set; } = "";
    public int Upvotes { get; set; }
    public bool IsPinned { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
