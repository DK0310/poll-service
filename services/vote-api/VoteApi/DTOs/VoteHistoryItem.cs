namespace VoteApi.DTOs;

/// <summary>One entry in a user's vote history: a poll they submitted answers to.</summary>
public record VoteHistoryItem
{
    public string PollCode { get; init; } = "";
    public string? Title { get; init; }
    public bool IsActive { get; init; }
    public int AnswerCount { get; init; }
    public DateTime VotedAt { get; init; }
}
