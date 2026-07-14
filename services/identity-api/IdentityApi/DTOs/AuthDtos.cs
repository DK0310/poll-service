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

// ── Email OTP + Google login ────────────────────────────────
public record RegisterResponse
{
    public bool RequiresVerification { get; init; } = true;
}

public record VerifyEmailRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = "";

    [Required]
    public string Code { get; init; } = "";
}

public record ResendCodeRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = "";

    [Required]
    public string Purpose { get; init; } = "";   // OtpPurpose.EmailVerification | PasswordReset
}

public record GoogleLoginRequest
{
    [Required]
    public string IdToken { get; init; } = "";
}

public record GoogleAuthResponse
{
    public string Token { get; init; } = "";
    public bool HasPassword { get; init; }        // false → prompt the user to set one
}

public record SetPasswordRequest
{
    [Required]
    public string Password { get; init; } = "";
}

public record ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = "";
}

public record ResetPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = "";

    [Required]
    public string Code { get; init; } = "";

    [Required]
    public string NewPassword { get; init; } = "";
}

public record ChangePasswordRequest
{
    // Empty/ignored when the account has no password yet (a first-time set).
    public string CurrentPassword { get; init; } = "";

    [Required]
    public string NewPassword { get; init; } = "";

    // Emailed OTP — required when changing an existing password; ignored on a first-time set.
    public string Code { get; init; } = "";
}

// ── Profile ─────────────────────────────────────────────────
public record ProfileResponse
{
    public Guid Id { get; init; }
    public string Email { get; init; } = "";
    public string? Username { get; init; }
    public string? Bio { get; init; }
    public string? AvatarUrl { get; init; }
    public string Role { get; init; } = "";
    public bool HasPassword { get; init; }
    public bool HasGoogle { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record UpdateProfileRequest
{
    [MaxLength(50)]
    public string? Username { get; init; }

    [MaxLength(300)]
    public string? Bio { get; init; }

    // A base64 data URL ("data:image/…;base64,…") or null to clear.
    public string? AvatarUrl { get; init; }
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
