namespace VoteApi.Models;

/// <summary>
/// One upvote on an audience question ("Ask") by one "voter key" (the logged-in user id when
/// present, otherwise the browser voter token). A unique index on (AudienceQuestionId, VoterKey)
/// enforces one upvote per person per question (RBAC: Ask upvote dedup).
/// </summary>
public class AudienceQuestionUpvote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AudienceQuestionId { get; set; }
    public string VoterKey { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
