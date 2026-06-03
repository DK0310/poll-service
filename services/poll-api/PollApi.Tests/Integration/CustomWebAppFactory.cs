using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PollApi.Data;

namespace PollApi.Tests.Integration;

/// <summary>
/// Spins up the real Poll API pipeline but swaps SQL Server for a per-factory in-memory
/// database. The startup auto-migration in Program.cs is skipped automatically because
/// the in-memory provider is not relational (`Database.IsRelational()` → false).
/// </summary>
public class CustomWebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            // Remove every DbContextOptions-related registration (options, non-generic,
            // and the EF-internal IDbContextOptionsConfiguration), then re-register InMemory.
            var toRemove = services
                .Where(d => d.ServiceType.Name.Contains("DbContextOptions"))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            var dbName = "PollTest_" + Guid.NewGuid();
            services.AddDbContext<PollDbContext>(o => o.UseInMemoryDatabase(dbName));
        });
    }
}
