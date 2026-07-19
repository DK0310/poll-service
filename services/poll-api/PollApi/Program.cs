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

// Create/upgrade the schema on startup, retrying while SQL Server comes up (docker-compose).
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PollDbContext>();
    if (db.Database.IsRelational())
    {
        // NOTE: destructive, deliberately gated. When the migration history was squashed the
        // fresh InitialCreate collides with tables that already exist (SQL error 2714). Setting
        // Migrations:AllowSchemaReset=true for ONE deploy drops every table and re-runs migrations.
        // Never leave this env var on in production: on the next boot it will wipe all data.
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

// Must be registered first so its try/catch wraps the whole pipeline.
app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();

// Drops all FK constraints first, then all tables, so DROP TABLE never trips over a still-referenced
// object. Lets a squashed migration rebuild the schema on a database that predates the squash.
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
