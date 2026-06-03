using System.ComponentModel.DataAnnotations;

namespace PollApi.DTOs;

public record CreatePollRequest
{
    [Required]
    public string Question { get; init; } = "";

    /// <summary>SingleChoice | YesNo | Rating | OpenText. Defaults to SingleChoice.</summary>
    public string Type { get; init; } = "SingleChoice";

    /// <summary>Options for SingleChoice polls. Ignored for YesNo/Rating (auto) and OpenText (none).</summary>
    public List<string> Options { get; init; } = new();

    /// <summary>Optional expiry in hours from creation. Null = no expiry.</summary>
    public int? ExpiryHours { get; init; }
}
