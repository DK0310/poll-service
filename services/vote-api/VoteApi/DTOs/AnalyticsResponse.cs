namespace VoteApi.DTOs;

public record AnalyticsResponse
{
    public string PollCode { get; init; } = "";
    public string? Title { get; init; }

    /// <summary>Number of distinct voters who submitted the survey.</summary>
    public int TotalVoters { get; init; }

    /// <summary>Per-minute submission counts (UTC), ascending — drives the votes-over-time line chart.</summary>
    public List<TimeBucket> Timeline { get; init; } = new();
    public TimeBucket? PeakMinute { get; init; }

    /// <summary>Per-question breakdown (leading option + total votes).</summary>
    public List<QuestionAnalytics> Questions { get; init; } = new();
}

public record QuestionAnalytics
{
    public Guid QuestionId { get; init; }
    public int QuestionIndex { get; init; }
    public string Text { get; init; } = "";
    public string Type { get; init; } = "SingleChoice";
    public int TotalVotes { get; init; }
    public TopOption? TopOption { get; init; }
}

public record TopOption
{
    public int OptionIndex { get; init; }
    public string Text { get; init; } = "";
    public int VoteCount { get; init; }
}

public record TimeBucket
{
    public DateTime Minute { get; init; }
    public int Count { get; init; }
}
