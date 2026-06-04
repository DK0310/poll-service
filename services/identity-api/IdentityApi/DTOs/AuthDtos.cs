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

// ── Admin user management ───────────────────────────────────
public record AdminUserResponse
{
    public Guid Id { get; init; }
    public string Email { get; init; } = "";
    public string Role { get; init; } = "";
    public DateTime CreatedAt { get; init; }
}

public record SetRoleRequest
{
    [Required]
    public string Role { get; init; } = "";
}
