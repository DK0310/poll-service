using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PollApi.Data;
using PollApi.Middleware;
using PollApi.Repositories;
using PollApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PollDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default"),
        sql => sql.EnableRetryOnFailure()));

builder.Services.AddScoped<PollRepository>();
builder.Services.AddScoped<PollService>();
builder.Services.AddHostedService<PollCleanupService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply EF Core migrations on startup, retrying while SQL Server starts (docker-compose).
// Skipped for non-relational providers (the in-memory DB used by integration tests).
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PollDbContext>();
    if (db.Database.IsRelational())
    {
        // Opt-in, temporary escape hatch: squashing migration history (see the multi-question
        // refactor) leaves __EFMigrationsHistory empty while the old tables still exist, so the
        // fresh "InitialCreate" collides (SQL error 2714, "already an object named ..."). Setting
        // Migrations:AllowSchemaReset (env Migrations__AllowSchemaReset=true) for a single deploy
        // wipes the stale schema once and retries — remove the env var again once it's applied.
        var canResetOnCollision = builder.Configuration.GetValue("Migrations:AllowSchemaReset", false);
        for (var attempt = 1; ; attempt++)
        {
            try { await db.Database.MigrateAsync(); break; }
            catch (SqlException ex) when (canResetOnCollision && ex.Number == 2714)
            {
                canResetOnCollision = false;
                app.Logger.LogWarning(ex,
                    "Migrations:AllowSchemaReset is set — migration history is empty but the schema " +
                    "already exists; dropping all tables and retrying.");
                await DropAllTablesAsync(db.Database);
            }
            catch (Exception ex) when (attempt < 12)
            {
                app.Logger.LogWarning(ex, "Database not ready (attempt {Attempt}); retrying in 5s…", attempt);
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }
}

// Unhandled-exception JSON handler (must be first in the pipeline)
app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();

// Drops every FK constraint then every table, so a squashed migration can recreate the schema
// from scratch on a database that predates the squash. Order-independent — the FK pass runs first
// so DROP TABLE never hits a still-referenced object.
static async Task DropAllTablesAsync(DatabaseFacade database)
{
    await database.ExecuteSqlRawAsync("""
        DECLARE @sql NVARCHAR(MAX) = N'';
        SELECT @sql += 'ALTER TABLE ' + QUOTENAME(s.name) + '.' + QUOTENAME(t.name) +
            ' DROP CONSTRAINT ' + QUOTENAME(fk.name) + ';'
        FROM sys.foreign_keys fk
        JOIN sys.tables t ON fk.parent_object_id = t.object_id
        JOIN sys.schemas s ON t.schema_id = s.schema_id;
        EXEC sp_executesql @sql;
        """);

    await database.ExecuteSqlRawAsync("""
        DECLARE @sql NVARCHAR(MAX) = N'';
        SELECT @sql += 'DROP TABLE ' + QUOTENAME(s.name) + '.' + QUOTENAME(t.name) + ';'
        FROM sys.tables t
        JOIN sys.schemas s ON t.schema_id = s.schema_id;
        EXEC sp_executesql @sql;
        """);
}

// Exposed so integration tests (Phase 9) can use WebApplicationFactory<Program>.
public partial class Program { }
