using IdentityApi.Data;
using IdentityApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IdentityApi.Tests.Integration;

public class CustomWebAppFactory : WebApplicationFactory<Program>
{
    // Shared fake so tests can read back the OTP that was "emailed".
    public FakeEmailSender Email { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Testing env doesn't load User Secrets, so supply a valid (>=32 char) JWT key
        // for token generation; the committed appsettings placeholder is intentionally short.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "integration-test-secret-key-at-least-32-characters!",
                ["Google:ClientId"] = "test-client-id.apps.googleusercontent.com"
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

            // Never hit real SMTP / Google in tests.
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Email);
            services.RemoveAll<IGoogleTokenVerifier>();
            services.AddSingleton<IGoogleTokenVerifier, FakeGoogleTokenVerifier>();
        });
    }
}
