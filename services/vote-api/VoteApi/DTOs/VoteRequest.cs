using System.ComponentModel.DataAnnotations;

namespace VoteApi.DTOs;

public record VoteRequest
{
    public int OptionIndex { get; init; }

    [Required]
    public string VoterToken { get; init; } = "";
}
