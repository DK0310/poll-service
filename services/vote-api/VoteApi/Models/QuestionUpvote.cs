namespace VoteApi.Models;

/// <summary>
/// One upvote on a Q&A question by one "voter key" (the logged-in user id when present,
/// otherwise the browser voter token). A unique index on (QuestionId, VoterKey) enforces
/// one upvote per person per question (RBAC: Q&A upvote dedup).
/// </summary>
public class QuestionUpvote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuestionId { get; set; }
    public string VoterKey { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
