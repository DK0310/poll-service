namespace VoteApi.DTOs;

public record VoteResultsResponse
{
    public string PollCode { get; init; } = "";
    public string? Title { get; init; }
    public bool IsActive { get; init; }

    /// <summary>Number of distinct voters who submitted the survey.</summary>
    public int TotalVoters { get; init; }

    public List<QuestionResults> Questions { get; init; } = new();
}

/// <summary>Live results for one survey question.</summary>
public record QuestionResults
{
    public Guid QuestionId { get; init; }
    public int QuestionIndex { get; init; }
    public string Text { get; init; } = "";
    public string Type { get; init; } = "SingleChoice";
    public int TotalVotes { get; init; }
    public List<OptionResult> Options { get; init; } = new();

    /// <summary>Submitted free-text answers for an OpenText question (empty otherwise).</summary>
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
