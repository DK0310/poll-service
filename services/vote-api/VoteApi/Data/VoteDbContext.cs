using Microsoft.EntityFrameworkCore;
using VoteApi.Models;

namespace VoteApi.Data;

public class VoteDbContext : DbContext
{
    public VoteDbContext(DbContextOptions<VoteDbContext> options) : base(options) { }

    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<AudienceQuestion> AudienceQuestions => Set<AudienceQuestion>();
    public DbSet<AudienceQuestionUpvote> AudienceQuestionUpvotes => Set<AudienceQuestionUpvote>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AudienceQuestion>(e =>
        {
            e.ToTable("AudienceQuestions");
            e.HasKey(q => q.Id);
            e.Property(q => q.Id).HasDefaultValueSql("NEWID()");
            e.Property(q => q.PollCode).HasMaxLength(16).IsRequired();
            e.Property(q => q.Text).HasMaxLength(1000).IsRequired();
            e.Property(q => q.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(q => q.PollCode);   // list questions for a poll
        });

        b.Entity<AudienceQuestionUpvote>(e =>
        {
            e.ToTable("AudienceQuestionUpvotes");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasDefaultValueSql("NEWID()");
            e.Property(u => u.VoterKey).HasMaxLength(128).IsRequired();
            e.Property(u => u.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            // One upvote per voter per question (RBAC dedup).
            e.HasIndex(u => new { u.AudienceQuestionId, u.VoterKey }).IsUnique();
        });

        b.Entity<Vote>(e =>
        {
            e.ToTable("Votes");
            e.HasKey(v => v.Id);
            e.Property(v => v.Id).HasDefaultValueSql("NEWID()");
            e.Property(v => v.PollCode).HasMaxLength(16).IsRequired();
            e.Property(v => v.VoterToken).HasMaxLength(128).IsRequired();
            e.Property(v => v.TextAnswer).HasMaxLength(1000);
            e.Property(v => v.AuthorName).HasMaxLength(64);
            e.Property(v => v.AuthorRole).HasMaxLength(20);
            e.Property(v => v.VotedAt).HasDefaultValueSql("GETUTCDATE()");

            // One vote per voter per question (a voter answers every question once)
            e.HasIndex(v => new { v.PollCode, v.QuestionId, v.VoterToken }).IsUnique();
            // Per-question vote-count aggregation
            e.HasIndex(v => new { v.PollCode, v.QuestionId, v.OptionIndex });
            // Analytics (votes over time)
            e.HasIndex(v => v.VotedAt);
            // Per-account vote history
            e.HasIndex(v => v.UserId);
        });
    }
}
