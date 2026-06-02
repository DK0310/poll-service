using Microsoft.EntityFrameworkCore;
using VoteApi.Models;

namespace VoteApi.Data;

public class VoteDbContext : DbContext
{
    public VoteDbContext(DbContextOptions<VoteDbContext> options) : base(options) { }

    public DbSet<Vote> Votes => Set<Vote>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Vote>(e =>
        {
            e.ToTable("Votes");
            e.HasKey(v => v.Id);
            e.Property(v => v.Id).HasDefaultValueSql("NEWID()");
            e.Property(v => v.PollCode).HasMaxLength(16).IsRequired();
            e.Property(v => v.VoterToken).HasMaxLength(128).IsRequired();
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
