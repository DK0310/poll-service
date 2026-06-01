using PollApi.Models;

namespace PollApi.DTOs;

public record OptionResponse
{
    public int OptionIndex { get; init; }
    public string Text { get; init; } = "";
}

public record PollResponse
{
    public string Code { get; init; } = "";
    public string Question { get; init; } = "";
    public string Status { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public bool IsActive { get; init; }
    public List<OptionResponse> Options { get; init; } = new();
    public string Url => $"/poll/{Code}";

    public static PollResponse From(Poll e) => new()
    {
        Code = e.Code,
        Question = e.Question,
        Status = e.Status.ToString(),
        CreatedAt = e.CreatedAt,
        ExpiresAt = e.ExpiresAt,
        IsActive = e.IsActive,
        Options = e.Options
            .OrderBy(o => o.OptionIndex)
            .Select(o => new OptionResponse { OptionIndex = o.OptionIndex, Text = o.Text })
            .ToList()
    };
}
