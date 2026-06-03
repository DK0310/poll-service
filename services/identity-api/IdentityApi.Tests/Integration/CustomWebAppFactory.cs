using IdentityApi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityApi.Tests.Integration;

public class CustomWebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Testing env doesn't load User Secrets, so supply a valid (>=32 char) JWT key
        // for token generation; the committed appsettings placeholder is intentionally short.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "integration-test-secret-key-at-least-32-characters!"
            });
        });

        builder.ConfigureServices(services =>
        {
            var toRemove = services
                .Where(d => d.ServiceType.Name.Contains("DbContextOptions"))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            var dbName = "IdentityTest_" + Guid.NewGuid();
            services.AddDbContext<IdentityDbContext>(o => o.UseInMemoryDatabase(dbName));
        });
    }
}
