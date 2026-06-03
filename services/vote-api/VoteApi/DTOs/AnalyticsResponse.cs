namespace VoteApi.DTOs;

public record AnalyticsResponse
{
    public string PollCode { get; init; } = "";
    public string Question { get; init; } = "";
    public int TotalVotes { get; init; }
    public TopOption? TopOption { get; init; }
    public TimeBucket? PeakMinute { get; init; }

    /// <summary>Per-minute vote counts (UTC), ascending — drives the votes-over-time line chart.</summary>
    public List<TimeBucket> Timeline { get; init; } = new();
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
