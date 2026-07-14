namespace VoteApi.Models;

public class Vote
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Which poll this vote belongs to. NOT a FK — the Polls table lives in PollDb.</summary>
    public string PollCode { get; set; } = "";

    /// <summary>Which survey question this answer is for. NOT a FK — Questions live in PollDb;
    /// the id is learned from the Poll API and trusted after validation.</summary>
    public Guid QuestionId { get; set; }
    public int OptionIndex { get; set; }

    /// <summary>Free-text answer for OpenText polls (null for choice/rating polls — stored, not tallied).</summary>
    public string? TextAnswer { get; set; }

    /// <summary>Display name for an OpenText answer's author (email local-part); null = anonymous guest.</summary>
    public string? AuthorName { get; set; }

    /// <summary>Author's role for an OpenText answer ("User" / "Admin"); null = anonymous guest.</summary>
    public string? AuthorRole { get; set; }

    /// <summary>Browser fingerprint / session token used to enforce one vote per voter per poll.</summary>
    public string VoterToken { get; set; } = "";

    /// <summary>The logged-in voter's id (from the Gateway's X-User-Id), when present — powers
    /// per-account vote history. NOT a FK — Users live in IdentityDb. Null for anonymous guests.</summary>
    public Guid? UserId { get; set; }

    public DateTime VotedAt { get; set; } = DateTime.UtcNow;
}
