using System.ComponentModel.DataAnnotations;

namespace VoteApi.DTOs;

public record VoteRequest
{
    public int OptionIndex { get; init; }

    /// <summary>Required for OpenText polls; ignored for choice/rating polls.</summary>
    public string? TextAnswer { get; init; }

    [Required]
    public string VoterToken { get; init; } = "";
}
