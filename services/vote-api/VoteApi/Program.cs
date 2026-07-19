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

// AllowCredentials is required for the SignalR websocket. Browsers reach the hub through the
// gateway, so the allowed origin is the gateway URL.
builder.Services.AddCors(opt => opt.AddPolicy(SignalRCors, p =>
    p.WithOrigins(builder.Configuration["Gateway:Url"] ?? "http://localhost:5000")
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));

// vote-api validates every vote against the poll by calling poll-api. Base address is a docker
// service name in compose (http://poll-api:8080), with a localhost fallback for running locally.
builder.Services.AddHttpClient<PollClientService>(client =>
{
    var pollApi = builder.Configuration["Services:PollApi"] ?? "http://localhost:5001";
    client.BaseAddress = new Uri(pollApi);
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Create/upgrade the schema on startup, retrying while SQL Server comes up (docker-compose).
// Skipped for the non-relational in-memory DB the integration tests use.
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VoteDbContext>();
    if (db.Database.IsRelational() && !EF.IsDesignTime)
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

app.UseCors(SignalRCors);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.MapHub<PollHub>("/hubs/poll");

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
