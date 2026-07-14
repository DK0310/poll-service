using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using VoteApi.Data;
using VoteApi.Hubs;
using VoteApi.Middleware;
using VoteApi.Repositories;
using VoteApi.Services;

var builder = WebApplication.CreateBuilder(args);

const string SignalRCors = "SignalR";

builder.Services.AddDbContext<VoteDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default"),
        sql => sql.EnableRetryOnFailure()));

builder.Services.AddScoped<VoteRepository>();
builder.Services.AddScoped<VoteService>();
builder.Services.AddScoped<AskRepository>();
builder.Services.AddScoped<AskService>();
builder.Services.AddSignalR();

// CORS for SignalR — credentials must be allowed for the WebSocket.
// In practice the browser connects via the Gateway (which also sets CORS); this is the
// documented origin for direct/proxied SignalR traffic (ARCHITECTURE → Environment Config).
builder.Services.AddCors(opt => opt.AddPolicy(SignalRCors, p =>
    p.WithOrigins(builder.Configuration["Gateway:Url"] ?? "http://localhost:5000")
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));

// Typed HttpClient for the inter-service call to the Poll API.
// Base address comes from config (docker: http://poll-api:8080); localhost fallback for dev.
builder.Services.AddHttpClient<PollClientService>(client =>
{
    var pollApi = builder.Configuration["Services:PollApi"] ?? "http://localhost:5001";
    client.BaseAddress = new Uri(pollApi);
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply EF Core migrations on startup, retrying while SQL Server starts (docker-compose).
// Skipped for non-relational providers (the in-memory DB used by integration tests).
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VoteDbContext>();
    if (db.Database.IsRelational() && !EF.IsDesignTime)
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

app.UseCors(SignalRCors);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.MapHub<PollHub>("/hubs/poll");

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
