using Microsoft.EntityFrameworkCore;
using PollApi.Models;

namespace PollApi.Data;

public class PollDbContext : DbContext
{
    public PollDbContext(DbContextOptions<PollDbContext> options) : base(options) { }

    public DbSet<Poll> Polls => Set<Poll>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<PollOption> PollOptions => Set<PollOption>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Poll>(e =>
        {
            e.ToTable("Polls");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasDefaultValueSql("NEWID()");
            e.Property(p => p.Code).HasMaxLength(16).IsRequired();
            e.HasIndex(p => p.Code).IsUnique();                 // primary lookup
            e.Property(p => p.Title).HasMaxLength(500);          // optional survey title
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(p => p.CreatorId);                        // "my polls" query
            e.HasIndex(p => p.ExpiresAt);                        // cleanup query

            // Computed properties are domain logic, not columns
            e.Ignore(p => p.IsExpired);
            e.Ignore(p => p.IsClosed);
            e.Ignore(p => p.IsActive);
        });

        b.Entity<Question>(e =>
        {
            e.ToTable("Questions");
            e.HasKey(q => q.Id);
            e.Property(q => q.Id).HasDefaultValueSql("NEWID()");
            e.Property(q => q.Text).HasMaxLength(500).IsRequired();
            e.Property(q => q.Type).HasConversion<string>().HasMaxLength(20);
            e.HasOne(q => q.Poll)
             .WithMany(p => p.Questions)
             .HasForeignKey(q => q.PollId)
             .OnDelete(DeleteBehavior.Cascade);                 // delete questions with the poll
            e.HasIndex(q => new { q.PollId, q.QuestionIndex });  // ordered question lookup
        });

        b.Entity<PollOption>(e =>
        {
            e.ToTable("PollOptions");
            e.HasKey(o => o.Id);
            e.Property(o => o.Id).HasDefaultValueSql("NEWID()");
            e.Property(o => o.Text).HasMaxLength(500).IsRequired();
            e.HasOne(o => o.Question)
             .WithMany(q => q.Options)
             .HasForeignKey(o => o.QuestionId)
             .OnDelete(DeleteBehavior.Cascade);                 // delete options with the question
            e.HasIndex(o => new { o.QuestionId, o.OptionIndex }); // ordered option lookup
        });
    }
}
