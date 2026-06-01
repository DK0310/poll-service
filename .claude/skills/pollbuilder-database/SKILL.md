---
name: pollbuilder-database
description: Use when modifying database schema, writing queries, or implementing migrations in any microservice
---

# PollBuilder Database Skill

## Overview

PollBuilder uses a **database-per-service** pattern. Each microservice owns its data and has its own SQL Server database with its own EF Core DbContext and migrations. No service directly accesses another service's database — inter-service data access happens via HTTP API calls.

**Core principles:**
- Each service owns its data exclusively
- Database structure enforces data integrity
- Migrations make schema changes reproducible across environments
- Services communicate via APIs, never via shared databases

---

## Database Ownership

| Service | Database | DbContext | Tables |
|---|---|---|---|
| **Poll API** | `PollDb` | `PollDbContext` | `Polls`, `PollOptions` |
| **Vote API** | `VoteDb` | `VoteDbContext` | `Votes` |
| **Identity API** | `IdentityDb` | `IdentityDbContext` | `Users` |

> **Why separate databases?** Each service can be deployed, scaled, and migrated independently. A schema change in Vote API doesn't affect Poll API.

---

## Tech Stack

| Component | Version | Purpose |
|---|---|---|
| **Engine** | SQL Server 2022 | Database management |
| **ORM** | EF Core 8 | C# to SQL mapping |
| **Dev Database** | LocalDB or Docker | Local development |
| **Prod Database** | SQL Server 2022 | Container on Render / Railway |

> **Dev shortcut:** In development, all three databases can live in the same SQL Server instance as separate databases. In production, they can be separate instances or separate databases on the same server.

---

## The Iron Law

```
MIGRATIONS FIRST, SCHEMA CHANGES SECOND
```

Each service maintains its own migration history. Run migrations per-service:

```bash
# Poll API migrations
dotnet ef migrations add InitialCreate \
  --project services/poll-api/PollApi \
  --startup-project services/poll-api/PollApi

# Vote API migrations
dotnet ef migrations add InitialCreate \
  --project services/vote-api/VoteApi \
  --startup-project services/vote-api/VoteApi

# Identity API migrations
dotnet ef migrations add InitialCreate \
  --project services/identity-api/IdentityApi \
  --startup-project services/identity-api/IdentityApi
```

---

## Poll API — Database Design

### Entities

```csharp
// services/poll-api/PollApi/Models/Poll.cs
public enum PollStatus { Open, Closed }

public class Poll
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Question { get; set; } = "";
    public PollStatus Status { get; set; } = PollStatus.Open;
    public DateTime? ExpiresAt { get; set; }
    public Guid? CreatorId { get; set; }    // From JWT — NOT a FK (no Users table here)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<PollOption> Options { get; set; } = new List<PollOption>();

    // Computed (not persisted)
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt < DateTime.UtcNow;
    public bool IsClosed => Status == PollStatus.Closed;
    public bool IsActive => !IsExpired && !IsClosed;
}

// services/poll-api/PollApi/Models/PollOption.cs
public class PollOption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PollId { get; set; }
    public int OptionIndex { get; set; }
    public string Text { get; set; } = "";

    // Navigation
    public Poll Poll { get; set; } = null!;
}
```

### DbContext

```csharp
// services/poll-api/PollApi/Data/PollDbContext.cs
public class PollDbContext : DbContext
{
    public PollDbContext(DbContextOptions<PollDbContext> options) : base(options) { }

    public DbSet<Poll> Polls => Set<Poll>();
    public DbSet<PollOption> PollOptions => Set<PollOption>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Poll>(e =>
        {
            e.ToTable("Polls");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasDefaultValueSql("NEWID()");
            e.HasIndex(p => p.Code).IsUnique();
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(p => p.CreatorId);         // For "my polls" query
            e.HasIndex(p => p.ExpiresAt);          // For cleanup query

            // Ignore computed properties
            e.Ignore(p => p.IsExpired);
            e.Ignore(p => p.IsClosed);
            e.Ignore(p => p.IsActive);
        });

        b.Entity<PollOption>(e =>
        {
            e.ToTable("PollOptions");
            e.HasKey(o => o.Id);
            e.Property(o => o.Id).HasDefaultValueSql("NEWID()");
            e.HasOne(o => o.Poll)
             .WithMany(p => p.Options)
             .HasForeignKey(o => o.PollId)
             .OnDelete(DeleteBehavior.Cascade);  // Delete options when poll deleted
            e.HasIndex(o => new { o.PollId, o.OptionIndex });
        });
    }
}
```

### Key design: CreatorId is NOT a foreign key

```
Poll API stores CreatorId as a plain Guid — NOT a FK to any Users table.
The Users table lives in Identity API's database (IdentityDb).

Poll API receives CreatorId from the X-User-Id header (set by the Gateway
after JWT validation). It stores this value but cannot JOIN to Users.

This is the microservice tradeoff: data isolation vs. query convenience.
```

---

## Vote API — Database Design

### Entity

```csharp
// services/vote-api/VoteApi/Models/Vote.cs
public class Vote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PollCode { get; set; } = "";   // NOT a FK — different database
    public int OptionIndex { get; set; }
    public string VoterToken { get; set; } = "";
    public DateTime VotedAt { get; set; } = DateTime.UtcNow;
}
```

### DbContext

```csharp
// services/vote-api/VoteApi/Data/VoteDbContext.cs
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

            // One vote per voter per poll
            e.HasIndex(v => new { v.PollCode, v.VoterToken }).IsUnique();

            // For vote count aggregation queries
            e.HasIndex(v => new { v.PollCode, v.OptionIndex });

            e.Property(v => v.VotedAt).HasDefaultValueSql("GETUTCDATE()");
        });
    }
}
```

### Key design: PollCode is a string, NOT a foreign key

```
Vote API stores PollCode as a plain string — NOT a FK to any Polls table.
The Polls table lives in Poll API's database (PollDb).

Vote API validates that a poll exists by calling Poll API over HTTP
(via PollClientService) before accepting a vote.

If the Poll API is temporarily down, Vote API returns an error
instead of accepting potentially invalid votes.
```

---

## Identity API — Database Design

### Entity

```csharp
// services/identity-api/IdentityApi/Models/User.cs
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

### DbContext

```csharp
// services/identity-api/IdentityApi/Data/IdentityDbContext.cs
public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasDefaultValueSql("NEWID()");
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });
    }
}
```

---

## Query Patterns

### Poll API Queries

```csharp
// Get poll with options
var poll = await _db.Polls
    .Include(p => p.Options.OrderBy(o => o.OptionIndex))
    .FirstOrDefaultAsync(p => p.Code == code);

// Get creator's polls
var polls = await _db.Polls
    .Include(p => p.Options)
    .Where(p => p.CreatorId == userId)
    .OrderByDescending(p => p.CreatedAt)
    .Take(50)
    .ToListAsync();

// Get expired but still open polls (for cleanup)
var expired = await _db.Polls
    .Where(p => p.Status == PollStatus.Open
             && p.ExpiresAt != null
             && p.ExpiresAt < DateTime.UtcNow)
    .ToListAsync();
```

### Vote API Queries

```csharp
// Check duplicate vote
var hasVoted = await _db.Votes
    .AnyAsync(v => v.PollCode == pollCode && v.VoterToken == voterToken);

// Aggregate vote counts (done in SQL, not in memory)
var voteCounts = await _db.Votes
    .Where(v => v.PollCode == pollCode)
    .GroupBy(v => v.OptionIndex)
    .Select(g => new VoteCount { OptionIndex = g.Key, Count = g.Count() })
    .ToListAsync();

// Votes over time (for analytics)
var votesOverTime = await _db.Votes
    .Where(v => v.PollCode == pollCode)
    .OrderBy(v => v.VotedAt)
    .Select(v => new { v.VotedAt, v.OptionIndex })
    .ToListAsync();
```

### Performance: Aggregate in SQL

```csharp
// ✅ GOOD: Aggregation in database (one query, O(1) memory)
var voteCounts = await _db.Votes
    .Where(v => v.PollCode == code)
    .GroupBy(v => v.OptionIndex)
    .Select(g => new VoteCount { OptionIndex = g.Key, Count = g.Count() })
    .ToListAsync();

// ❌ BAD: Load all votes into memory then count (O(n) memory)
var votes = await _db.Votes.Where(v => v.PollCode == code).ToListAsync();
var counts = votes.GroupBy(v => v.OptionIndex).Select(...);
```

---

## Connection Strings

### Per-service connection strings (docker-compose)

```yaml
# docker-compose.yml
services:
  poll-api:
    environment:
      ConnectionStrings__Default: "Server=db,1433;Database=PollDb;User Id=sa;Password=YourPassword123!;TrustServerCertificate=True;"

  vote-api:
    environment:
      ConnectionStrings__Default: "Server=db,1433;Database=VoteDb;User Id=sa;Password=YourPassword123!;TrustServerCertificate=True;"

  identity-api:
    environment:
      ConnectionStrings__Default: "Server=db,1433;Database=IdentityDb;User Id=sa;Password=YourPassword123!;TrustServerCertificate=True;"
```

> **Same SQL Server, different databases.** All three services connect to the same `db` container but use different `Database=` values. EF Core migrations create each database independently.

### Local development (appsettings.json per service)

```json
{
  "ConnectionStrings": {
    "Default": "Server=(localdb)\\mssqllocaldb;Database=PollDb;Trusted_Connection=True;"
  }
}
```

---

## Migration Workflow

### Per-service migrations

```bash
# ALWAYS run from the service directory

# Poll API
cd services/poll-api
dotnet ef migrations add InitialCreate --project PollApi
dotnet ef database update --project PollApi

# Vote API
cd services/vote-api
dotnet ef migrations add InitialCreate --project VoteApi
dotnet ef database update --project VoteApi

# Identity API
cd services/identity-api
dotnet ef migrations add InitialCreate --project IdentityApi
dotnet ef database update --project IdentityApi
```

### Inside Docker

```bash
# Apply migrations for each service
docker-compose exec poll-api dotnet ef database update
docker-compose exec vote-api dotnet ef database update
docker-compose exec identity-api dotnet ef database update
```

### Rolling back

```bash
dotnet ef migrations remove  # Remove last migration
dotnet ef database update [previous-migration-name]  # Revert
```

---

## Index Strategy

### Poll API Indexes

```
□ Polls.Code (UNIQUE) — primary lookup
□ Polls.CreatorId — "my polls" queries
□ Polls.ExpiresAt — cleanup service queries
□ PollOptions.(PollId, OptionIndex) — ordered option lookup
```

### Vote API Indexes

```
□ Votes.(PollCode, VoterToken) (UNIQUE) — duplicate vote prevention
□ Votes.(PollCode, OptionIndex) — vote count aggregation
□ Votes.VotedAt — analytics queries (votes over time)
```

### Identity API Indexes

```
□ Users.Email (UNIQUE) — login lookup
```

---

## Data Integrity Constraints

### Poll API

```
NOT NULL: Code, Question, Status
UNIQUE:  Code
CASCADE: PollOptions deleted when Poll deleted
CHECK:   OptionIndex >= 0
```

### Vote API

```
NOT NULL: PollCode, OptionIndex, VoterToken, VotedAt
UNIQUE:  (PollCode, VoterToken) — one vote per person per poll
CHECK:   OptionIndex >= 0
```

### Identity API

```
NOT NULL: Email, PasswordHash
UNIQUE:  Email
```

---

## Cross-Service Data Access Pattern

```
WRONG: Vote API queries PollDb directly
  ❌ await _pollDb.Polls.FirstOrDefaultAsync(p => p.Code == code);

RIGHT: Vote API calls Poll API over HTTP
  ✅ var poll = await _pollClient.GetPollAsync(code);
     // Uses typed HttpClient: http://poll-api:8080/api/polls/{code}
```

### When Vote API needs poll data:

```csharp
// services/vote-api/VoteApi/Services/PollClientService.cs
public class PollClientService
{
    private readonly HttpClient _http;
    public PollClientService(HttpClient http) => _http = http;

    public async Task<PollInfo?> GetPollAsync(string code)
    {
        var response = await _http.GetAsync($"/api/polls/{code}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<PollInfo>();
    }
}
```

---

## Common Mistakes

❌ **DON'T:**
- Share a database between services (breaks independence)
- Use foreign keys across service boundaries (different DBs)
- Manually edit databases without migrations
- Load all votes into memory to count them (use SQL aggregation)
- Store passwords in plain text (use BCrypt)
- Skip composite unique index on Votes (allows duplicate votes)
- Forget cascade deletes on PollOptions (orphaned records)

✅ **DO:**
- One database per service with its own DbContext
- Use HTTP API calls for cross-service data
- Create migration per service for every schema change
- Use GroupBy + Count for vote aggregation in SQL
- Use computed properties for domain logic (IsActive)
- Test migrations on dev database first
- Document which service owns which data

---

## Cross-References

- **Using database in services** → See `pollbuilder-backend/SKILL.md`
- **System architecture** → See `pollbuilder-architecture/SKILL.md`
- **Deploying databases** → See `pollbuilder-devops/SKILL.md`
