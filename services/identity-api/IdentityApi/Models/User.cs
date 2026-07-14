namespace IdentityApi.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string? PasswordHash { get; set; }     // null for Google-only accounts (no password set)
    public string? GoogleId { get; set; }          // Google "sub"; links the external identity
    public bool EmailVerified { get; set; }        // false until an OTP confirms it (Google logins are pre-verified)
    public string? Username { get; set; }          // display name; defaults to the email local-part
    public string? Bio { get; set; }               // short profile blurb
    public string? AvatarUrl { get; set; }         // self-contained base64 data URL (deployment-safe, no blob store)
    public string Role { get; set; } = "User";   // "User" | "Admin"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
