namespace VoteApi.DTOs;

public record VoteResultsResponse
{
    public string PollCode { get; init; } = "";
    public string Question { get; init; } = "";
    public int TotalVotes { get; init; }
    public bool IsActive { get; init; }
    public List<OptionResult> Options { get; init; } = new();
}

public record OptionResult
{
    public int OptionIndex { get; init; }
    public string Text { get; init; } = "";
    public int VoteCount { get; init; }
    public double Percentage { get; init; }
}
