using System.ComponentModel.DataAnnotations;
using VoteApi.Models;

namespace VoteApi.DTOs;

public record SubmitAskRequest
{
    [Required]
    public string Text { get; init; } = "";
}

// Upvote body — the browser voter token used to dedup upvotes for guests.
// (When the caller is logged in, the Gateway-set X-User-Id is used as the voter key instead.)
public record UpvoteRequest
{
    public string? VoterToken { get; init; }
}

public record AskResponse
{
    public Guid Id { get; init; }
    public string Text { get; init; } = "";
    public int Upvotes { get; init; }
    public bool IsPinned { get; init; }
    public DateTime CreatedAt { get; init; }

    public static AskResponse From(AudienceQuestion q) => new()
    {
        Id = q.Id,
        Text = q.Text,
        Upvotes = q.Upvotes,
        IsPinned = q.IsPinned,
        CreatedAt = q.CreatedAt
    };
}
