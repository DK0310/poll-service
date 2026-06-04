namespace IdentityApi.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "User";   // "User" | "Admin"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
