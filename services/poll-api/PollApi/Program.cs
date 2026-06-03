using Microsoft.EntityFrameworkCore;
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
        for (var attempt = 1; ; attempt++)
        {
            try { await db.Database.MigrateAsync(); break; }
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

// Exposed so integration tests (Phase 9) can use WebApplicationFactory<Program>.
public partial class Program { }
