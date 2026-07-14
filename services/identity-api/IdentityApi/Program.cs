using IdentityApi.Data;
using IdentityApi.Middleware;
using IdentityApi.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<IdentityDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default"),
        sql => sql.EnableRetryOnFailure()));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<IGoogleTokenVerifier, GoogleTokenVerifier>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply EF Core migrations on startup, retrying while SQL Server starts (docker-compose).
// Skipped for non-relational providers (the in-memory DB used by integration tests).
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    if (db.Database.IsRelational() && !EF.IsDesignTime)
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

    // Promote configured admin emails to the Admin role (idempotent). Admins register
    // normally first; this elevates them on startup. Config: Admin:Emails (env Admin__Emails__0, …).
    var adminEmails = (app.Configuration.GetSection("Admin:Emails").Get<string[]>() ?? [])
        .Select(e => e.Trim().ToLowerInvariant())
        .Where(e => e.Length > 0)
        .ToArray();
    if (adminEmails.Length > 0)
    {
        var toPromote = await db.Users
            .Where(u => adminEmails.Contains(u.Email) && u.Role != "Admin")
            .ToListAsync();
        foreach (var u in toPromote) u.Role = "Admin";
        if (toPromote.Count > 0)
        {
            await db.SaveChangesAsync();
            app.Logger.LogInformation("Promoted {Count} user(s) to Admin.", toPromote.Count);
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
