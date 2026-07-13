using System.ComponentModel.DataAnnotations;

namespace VoteApi.DTOs;

/// <summary>
/// A batch submission for a survey: the voter answers every question, then submits once.
/// One submission per voter per poll.
/// </summary>
public record VoteRequest
{
    /// <summary>OpenText author display name (email local-part); null for guests. Display-only, client-supplied.</summary>
    public string? AuthorName { get; init; }

    /// <summary>OpenText author role ("User" / "Admin"); null for guests. Display-only, client-supplied.</summary>
    public string? AuthorRole { get; init; }

    [Required]
    public string VoterToken { get; init; } = "";

    /// <summary>One answer per question in the poll.</summary>
    [Required]
    public List<QuestionAnswer> Answers { get; init; } = new();
}

public record QuestionAnswer
{
    public Guid QuestionId { get; init; }

    /// <summary>Chosen option index for choice/rating/yes-no questions (ignored for OpenText).</summary>
    public int OptionIndex { get; init; }

    /// <summary>Required for OpenText questions; ignored otherwise.</summary>
    public string? TextAnswer { get; init; }
}
