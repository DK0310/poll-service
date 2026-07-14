using IdentityApi.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityApi.Data;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<VerificationCode> VerificationCodes => Set<VerificationCode>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasDefaultValueSql("NEWID()");
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();          // login lookup + uniqueness
            // PasswordHash is nullable (Google-only accounts have no password).
            e.Property(u => u.GoogleId).HasMaxLength(255);
            e.HasIndex(u => u.GoogleId)
                .IsUnique()
                .HasFilter("[GoogleId] IS NOT NULL");     // one account per Google identity
            e.Property(u => u.EmailVerified).HasDefaultValue(false);
            e.Property(u => u.Username).HasMaxLength(50);
            e.Property(u => u.Bio).HasMaxLength(300);
            // AvatarUrl holds a base64 data URL — no length cap (nvarchar(max)).
            e.Property(u => u.Role).HasMaxLength(20).IsRequired().HasDefaultValue("User");
            e.Property(u => u.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        b.Entity<VerificationCode>(e =>
        {
            e.ToTable("VerificationCodes");
            e.HasKey(v => v.Id);
            e.Property(v => v.Id).HasDefaultValueSql("NEWID()");
            e.Property(v => v.Email).HasMaxLength(256).IsRequired();
            e.Property(v => v.CodeHash).IsRequired();
            e.Property(v => v.Purpose).HasMaxLength(40).IsRequired();
            e.Property(v => v.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(v => new { v.Email, v.Purpose });  // newest-code lookup per flow
        });
    }
}
