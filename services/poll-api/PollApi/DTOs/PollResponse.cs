using PollApi.Models;

namespace PollApi.DTOs;

public record OptionResponse
{
    public int OptionIndex { get; init; }
    public string Text { get; init; } = "";
}

public record QuestionResponse
{
    public Guid Id { get; init; }
    public int QuestionIndex { get; init; }
    public string Text { get; init; } = "";
    public string Type { get; init; } = "";
    public List<OptionResponse> Options { get; init; } = new();

    public static QuestionResponse From(Question q) => new()
    {
        Id = q.Id,
        QuestionIndex = q.QuestionIndex,
        Text = q.Text,
        Type = q.Type.ToString(),
        Options = q.Options
            .OrderBy(o => o.OptionIndex)
            .Select(o => new OptionResponse { OptionIndex = o.OptionIndex, Text = o.Text })
            .ToList()
    };
}

public record PollResponse
{
    public string Code { get; init; } = "";
    public string? Title { get; init; }
    public string Status { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public bool IsActive { get; init; }
    public Guid? CreatorId { get; init; }   // owner — for ownership checks (Vote API analytics, frontend)
    public List<QuestionResponse> Questions { get; init; } = new();
    public string Url => $"/poll/{Code}";

    public static PollResponse From(Poll e) => new()
    {
        Code = e.Code,
        Title = e.Title,
        Status = e.Status.ToString(),
        CreatedAt = e.CreatedAt,
        ExpiresAt = e.ExpiresAt,
        IsActive = e.IsActive,
        CreatorId = e.CreatorId,
        Questions = e.Questions
            .OrderBy(q => q.QuestionIndex)
            .Select(QuestionResponse.From)
            .ToList()
    };
}
