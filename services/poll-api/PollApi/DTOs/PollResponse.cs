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
    public string Type { get; init; } = "";
    public string Status { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public bool IsActive { get; init; }
    public Guid? CreatorId { get; init; }   // owner — for ownership checks (Vote API analytics, frontend)
    public List<OptionResponse> Options { get; init; } = new();
    public string Url => $"/poll/{Code}";

    public static PollResponse From(Poll e) => new()
    {
        Code = e.Code,
        Question = e.Question,
        Type = e.Type.ToString(),
        Status = e.Status.ToString(),
        CreatedAt = e.CreatedAt,
        ExpiresAt = e.ExpiresAt,
        IsActive = e.IsActive,
        CreatorId = e.CreatorId,
        Options = e.Options
            .OrderBy(o => o.OptionIndex)
            .Select(o => new OptionResponse { OptionIndex = o.OptionIndex, Text = o.Text })
            .ToList()
    };
}
