using System.ComponentModel.DataAnnotations;

namespace PollApi.DTOs;

public record CreatePollRequest
{
    [Required]
    public string Question { get; init; } = "";

    [Required]
    public List<string> Options { get; init; } = new();

    /// <summary>Optional expiry in hours from creation. Null = no expiry.</summary>
    public int? ExpiryHours { get; init; }
}
