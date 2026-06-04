using Microsoft.EntityFrameworkCore;
using VoteApi.Models;

namespace VoteApi.Data;

public class VoteDbContext : DbContext
{
    public VoteDbContext(DbContextOptions<VoteDbContext> options) : base(options) { }

    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionUpvote> QuestionUpvotes => Set<QuestionUpvote>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Question>(e =>
        {
            e.ToTable("Questions");
            e.HasKey(q => q.Id);
            e.Property(q => q.Id).HasDefaultValueSql("NEWID()");
            e.Property(q => q.PollCode).HasMaxLength(16).IsRequired();
            e.Property(q => q.Text).HasMaxLength(1000).IsRequired();
            e.Property(q => q.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(q => q.PollCode);   // list questions for a poll
        });

        b.Entity<QuestionUpvote>(e =>
        {
            e.ToTable("QuestionUpvotes");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasDefaultValueSql("NEWID()");
            e.Property(u => u.VoterKey).HasMaxLength(128).IsRequired();
            e.Property(u => u.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            // One upvote per voter per question (RBAC dedup).
            e.HasIndex(u => new { u.QuestionId, u.VoterKey }).IsUnique();
        });

        b.Entity<Vote>(e =>
        {
            e.ToTable("Votes");
            e.HasKey(v => v.Id);
            e.Property(v => v.Id).HasDefaultValueSql("NEWID()");
            e.Property(v => v.PollCode).HasMaxLength(16).IsRequired();
            e.Property(v => v.VoterToken).HasMaxLength(128).IsRequired();
            e.Property(v => v.TextAnswer).HasMaxLength(1000);
            e.Property(v => v.VotedAt).HasDefaultValueSql("GETUTCDATE()");

            // One vote per voter per poll
            e.HasIndex(v => new { v.PollCode, v.VoterToken }).IsUnique();
            // Vote-count aggregation
            e.HasIndex(v => new { v.PollCode, v.OptionIndex });
            // Analytics (votes over time)
            e.HasIndex(v => v.VotedAt);
        });
    }
}
