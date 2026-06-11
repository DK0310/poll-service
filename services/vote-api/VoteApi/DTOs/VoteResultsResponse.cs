namespace VoteApi.DTOs;

public record VoteResultsResponse
{
    public string PollCode { get; init; } = "";
    public string Question { get; init; } = "";
    public string Type { get; init; } = "SingleChoice";
    public int TotalVotes { get; init; }
    public bool IsActive { get; init; }
    public List<OptionResult> Options { get; init; } = new();

    /// <summary>Submitted free-text answers for OpenText polls (empty for choice/rating polls).</summary>
    public List<TextAnswerResponse> TextAnswers { get; init; } = new();
}

/// <summary>One OpenText answer, rendered as a social-style comment. Author fields are null for guests.</summary>
public record TextAnswerResponse
{
    public string Text { get; init; } = "";
    public string? AuthorName { get; init; }
    public string? AuthorRole { get; init; }
    public DateTime VotedAt { get; init; }
}

public record OptionResult
{
    public int OptionIndex { get; init; }
    public string Text { get; init; } = "";
    public int VoteCount { get; init; }
    public double Percentage { get; init; }
}
