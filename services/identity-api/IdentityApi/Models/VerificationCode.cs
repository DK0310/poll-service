namespace IdentityApi.Models;

/// <summary>
/// A one-time email code (BCrypt-hashed, single-use, short-lived) used to verify a new
/// account's email or to reset a password. Keyed by normalized email + purpose.
/// </summary>
public class VerificationCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";                 // normalized (trim + lower)
    public string CodeHash { get; set; } = "";              // BCrypt hash of the 6-digit code
    public string Purpose { get; set; } = "";               // OtpPurpose.EmailVerification | PasswordReset
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }               // single-use marker
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class OtpPurpose
{
    public const string EmailVerification = "EmailVerification";
    public const string PasswordReset = "PasswordReset";
}
