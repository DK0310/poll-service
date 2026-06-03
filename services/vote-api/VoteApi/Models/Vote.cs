namespace VoteApi.Models;

public class Vote
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Which poll this vote belongs to. NOT a FK — the Polls table lives in PollDb.</summary>
    public string PollCode { get; set; } = "";
    public int OptionIndex { get; set; }

    /// <summary>Free-text answer for OpenText polls (null for choice/rating polls — stored, not tallied).</summary>
    public string? TextAnswer { get; set; }

    /// <summary>Browser fingerprint / session token used to enforce one vote per voter per poll.</summary>
    public string VoterToken { get; set; } = "";
    public DateTime VotedAt { get; set; } = DateTime.UtcNow;
}
