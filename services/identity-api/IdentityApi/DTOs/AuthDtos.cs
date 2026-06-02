using System.ComponentModel.DataAnnotations;

namespace IdentityApi.DTOs;

public record RegisterRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = "";

    [Required]
    public string Password { get; init; } = "";
}

public record LoginRequest
{
    [Required]
    public string Email { get; init; } = "";

    [Required]
    public string Password { get; init; } = "";
}

public record AuthResponse
{
    public string Token { get; init; } = "";
}
