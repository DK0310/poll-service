---
name: pollbuilder-database
description: Use when modifying database schema, writing queries, or implementing migrations in any microservice
---

# PollBuilder Database Skill

This skill holds **reusable EF Core 8 + SQL Server patterns**: how to model entities, configure a DbContext, run per-service migrations, write efficient queries, and choose ivkvndexes.

> **The concrete schema is in [ARCHITECTURE.md](../../../ARCHITECTURE.md)** — the authoritative database-ownership map, every table's columns/types, the index list, and connection-string values. This skill shows the *techniques*; consult ARCHITECTURE.md for the actual columns and indexes. The Poll API examples below are illustrative of each technique.

---

## Core Principles — Database Per Service

- **Each service owns its data exclusively.** One database, one DbContext, one migration history per service.
- **No cross-database access.** A service never opens a connection to another service's database and never defines a FK across the boundary. It fetches what it needs via an HTTP API call.
- **Migrations make schema reproducible** across every environment.
- **Why separate databases?** Each service can be deployed, scaled, and migrated independently — a schema change in one never touches another.

> In dev, the databases can share one SQL Server instance (different `Database=` values); EF Core migrations create each independently. See [ARCHITECTURE.md](../../../ARCHITECTURE.md) for the ownership map and connection strings.

---

## The Iron Law

```
MIGRATIONS FIRST, SCHEMA CHANGES SECOND
```

Never hand-edit a database. Every schema change is a migration, run per service so each keeps its own history.

---

## EF Core Configuration Patterns

Configure the schema in `OnModelCreating` (fluent API), not with scattered attributes. This Poll example illustrates every technique you'll reuse:

```csharp
protected override void OnModelCreating(ModelBuilder b)
{
    b.Entity<Poll>(e =>
    {
        e.ToTable("Polls");
        e.HasKey(p => p.Id);
        e.Property(p => p.Id).HasDefaultValueSql("NEWID()");        // DB-generated PK
        e.HasIndex(p => p.Code).IsUnique();                          // unique lookup key
        e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20); // enum → string
        e.Property(p => p.CreatedAt).HasDefaultValueSql("GETUTCDATE()");     // UTC default
        e.HasIndex(p => p.CreatorId);                                // index a query predicate
        e.HasIndex(p => p.ExpiresAt);
        e.Ignore(p => p.IsExpired);                                  // computed, not persisted
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
         .OnDelete(DeleteBehavior.Cascade);                          // children deleted with parent
        e.HasIndex(o => new { o.PollId, o.OptionIndex });            // composite index
    });
}
```

Techniques to reuse: DB-side defaults (`NEWID()`, `GETUTCDATE()`), unique & composite indexes, enum-to-string conversion with a max length, cascade delete for owned children, and `Ignore` for computed domain properties.

---

## Modeling Heuristics

- **Computed properties for domain logic, not columns.** Expose derived state (e.g. `IsActive => !IsExpired && !IsClosed`) as a getter and `Ignore()` it in the model — keep it out of the table.
- **Cross-service references are plain values, never FKs.** When an entity references something owned by another service, store the identifier as a plain `string`/`Guid` and validate it via an HTTP call. (In this project: `Vote.PollCode` is a string, `Poll.CreatorId` is a Guid — neither is a FK, because the referenced table lives in another service's database.) This is the microservice tradeoff: data isolation over JOIN convenience.
- **UTC everywhere.** Default timestamps to `GETUTCDATE()` and set `DateTime.UtcNow` in code.
- **Hash secrets, never store them.** Passwords are BCrypt hashes.

---

## Query Patterns

```csharp
// Load an aggregate with its ordered children
var poll = await _db.Polls
    .Include(p => p.Options.OrderBy(o => o.OptionIndex))
    .FirstOrDefaultAsync(p => p.Code == code);

// Existence check (cheaper than fetching)
var hasVoted = await _db.Votes
    .AnyAsync(v => v.PollCode == code && v.VoterToken == token);

// Cap unbounded list queries
var mine = await _db.Polls
    .Where(p => p.CreatorId == userId)
    .OrderByDescending(p => p.CreatedAt)
    .Take(50)
    .ToListAsync();
```

### Heuristic — Aggregate in the database, not in memory

```csharp
// ✅ GOOD: GROUP BY runs in SQL — one query, O(1) app memory
var counts = await _db.Votes
    .Where(v => v.PollCode == code)
    .GroupBy(v => v.OptionIndex)
    .Select(g => new VoteCount { OptionIndex = g.Key, Count = g.Count() })
    .ToListAsync();

// ❌ BAD: pull every row, then count in C# — O(n) memory, slow at scale
var votes = await _db.Votes.Where(v => v.PollCode == code).ToListAsync();
var counts = votes.GroupBy(v => v.OptionIndex)...;
```

---

## Choosing Indexes

Index the columns your real queries filter, join, or sort on — nothing speculative.

- **Unique index** for any natural key or "one X per Y" rule (e.g. a share code; one vote per voter per poll → unique composite).
- **Composite index** for multi-column predicates, ordered most-selective-first.
- **Single-column index** for frequent `WHERE`/`ORDER BY` predicates (a foreign-key-like column, an expiry timestamp, a login email).

The full per-table index list is in [ARCHITECTURE.md](../../../ARCHITECTURE.md). When you add a query, check whether it needs a new index and add it in the same migration.

---

## Migration Workflow

Run migrations **per service**, from that service's directory:

```bash
# Pattern — repeat per service (PollApi / VoteApi / IdentityApi)
cd services/poll-api
dotnet ef migrations add <Name> --project PollApi
dotnet ef database update --project PollApi
```

Inside Docker:

```bash
docker-compose exec poll-api     dotnet ef database update
docker-compose exec vote-api     dotnet ef database update
docker-compose exec identity-api dotnet ef database update
```

Rolling back:

```bash
dotnet ef migrations remove                       # undo the last (unapplied) migration
dotnet ef database update <PreviousMigrationName> # revert applied changes
```

---

## Cross-Service Data Access Pattern

```
❌ WRONG — Vote API reaching into PollDb directly
   await _pollDb.Polls.FirstOrDefaultAsync(p => p.Code == code);

✅ RIGHT — Vote API calls the Poll API over HTTP
   var poll = await _pollClient.GetPollAsync(code);   // typed HttpClient → http://poll-api:8080
```

If the owning service is unreachable, return a failure rather than guessing — never accept data that references an unvalidated entity. (Implementation of the typed client lives in `pollbuilder-backend`.)

---

## Connection Strings

Pattern: one connection string per service, all pointing at the same SQL Server host (`db`) but a different `Database=` value, so each service gets its own database. The actual strings and env-var names are in [ARCHITECTURE.md → Environment Configuration](../../../ARCHITECTURE.md).

```jsonc
// appsettings.json — local dev shape (value differs per service)
{ "ConnectionStrings": { "Default": "Server=...;Database=<ServiceDb>;..." } }
```

---

## Common Mistakes

❌ **DON'T:**
- Share a database between services (breaks independence)
- Use foreign keys across service boundaries (different DBs)
- Hand-edit databases without a migration
- Load all rows into memory to count them (aggregate in SQL)
- Store passwords in plain text (use BCrypt)
- Skip the composite unique index that enforces a business rule (e.g. duplicate votes)
- Forget cascade deletes on owned children (orphaned rows)

✅ **DO:**
- One database + DbContext + migration history per service
- Fetch cross-service data via HTTP, never a shared connection
- Add a migration for every schema change, per service
- Use `GroupBy` + `Count` for aggregation in SQL
- Model derived state as `Ignore()`d computed properties
- Index the columns your queries actually use
- Keep the schema in ARCHITECTURE.md in sync with your migrations

---

## Cross-References

- **Authoritative schema, ownership, indexes, connection strings** → [ARCHITECTURE.md](../../../ARCHITECTURE.md)
- **Using the DbContext from services** → `pollbuilder-backend`
- **System design principles** → `pollbuilder-architecture`
- **Deploying databases & migrations in Docker** → `pollbuilder-devops`
- **Testing with in-memory databases** → `pollbuilder-testing`
