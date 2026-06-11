using System.ComponentModel.DataAnnotations;

namespace VoteApi.DTOs;

public record VoteRequest
{
    public int OptionIndex { get; init; }

    /// <summary>Required for OpenText polls; ignored for choice/rating polls.</summary>
    public string? TextAnswer { get; init; }

    /// <summary>OpenText author display name (email local-part); null for guests. Display-only, client-supplied.</summary>
    public string? AuthorName { get; init; }

    /// <summary>OpenText author role ("User" / "Admin"); null for guests. Display-only, client-supplied.</summary>
    public string? AuthorRole { get; init; }

    [Required]
    public string VoterToken { get; init; } = "";
}
