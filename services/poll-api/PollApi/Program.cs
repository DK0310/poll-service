using Microsoft.EntityFrameworkCore;
using PollApi.Data;
using PollApi.Middleware;
using PollApi.Repositories;
using PollApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PollDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<PollRepository>();
builder.Services.AddScoped<PollService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

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
