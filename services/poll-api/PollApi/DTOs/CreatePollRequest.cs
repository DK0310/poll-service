using System.ComponentModel.DataAnnotations;

namespace PollApi.DTOs;

public record CreatePollRequest
{
    /// <summary>Optional survey title. Null/empty = untitled.</summary>
    public string? Title { get; init; }

    /// <summary>The survey's questions (at least one). Each owns its type and options.</summary>
    [Required]
    public List<CreateQuestionRequest> Questions { get; init; } = new();

    /// <summary>Optional expiry in hours from creation. Null = no expiry.</summary>
    public int? ExpiryHours { get; init; }
}

public record CreateQuestionRequest
{
    [Required]
    public string Text { get; init; } = "";

    /// <summary>SingleChoice | YesNo | Rating | OpenText. Defaults to SingleChoice.</summary>
    public string Type { get; init; } = "SingleChoice";

    /// <summary>Options for SingleChoice questions. Ignored for YesNo/Rating (auto) and OpenText (none).</summary>
    public List<string> Options { get; init; } = new();
}
