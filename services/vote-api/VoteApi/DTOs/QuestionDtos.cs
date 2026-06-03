using System.ComponentModel.DataAnnotations;
using VoteApi.Models;

namespace VoteApi.DTOs;

public record SubmitQuestionRequest
{
    [Required]
    public string Text { get; init; } = "";
}

public record QuestionResponse
{
    public Guid Id { get; init; }
    public string Text { get; init; } = "";
    public int Upvotes { get; init; }
    public bool IsPinned { get; init; }
    public DateTime CreatedAt { get; init; }

    public static QuestionResponse From(Question q) => new()
    {
        Id = q.Id,
        Text = q.Text,
        Upvotes = q.Upvotes,
        IsPinned = q.IsPinned,
        CreatedAt = q.CreatedAt
    };
}
