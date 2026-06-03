using Microsoft.EntityFrameworkCore;
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
builder.Services.AddScoped<QuestionRepository>();
builder.Services.AddScoped<QuestionService>();
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

app.UseCors(SignalRCors);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.MapHub<PollHub>("/hubs/poll");

app.Run();

// Exposed so integration tests (Phase 9) can use WebApplicationFactory<Program>.
public partial class Program { }
