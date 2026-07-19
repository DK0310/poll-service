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

// Send real email only when SMTP creds are set. Without them, LogEmailSender just writes the OTP
// to the console so the verification/reset flows still work locally without secrets.
const string SecretPlaceholder = "__SET_VIA_USER_SECRETS_OR_ENV__";
static bool IsConfigured(string? v) => !string.IsNullOrWhiteSpace(v) && v != SecretPlaceholder;
if (IsConfigured(builder.Configuration["Smtp:User"]) && IsConfigured(builder.Configuration["Smtp:Password"]))
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
else
    builder.Services.AddSingleton<IEmailSender, LogEmailSender>();
builder.Services.AddSingleton<IGoogleTokenVerifier, GoogleTokenVerifier>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Create/upgrade the schema on startup. In docker-compose the app boots before SQL Server is
// ready, so we retry for a while. The in-memory DB used by tests isn't relational, so skip it.
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

    // How the first admin is made: register normally, then list the email in Admin:Emails
    // (env Admin__Emails__0) and restart. This promotes matching users and is safe to re-run.
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

// Must be registered first so its try/catch wraps the whole pipeline.
app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();

// public + partial so the integration tests can spin this up via WebApplicationFactory<Program>.
public partial class Program { }
