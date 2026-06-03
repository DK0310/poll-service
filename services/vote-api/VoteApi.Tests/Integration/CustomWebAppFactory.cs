using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VoteApi.Data;
using VoteApi.Services;

namespace VoteApi.Tests.Integration;

/// <summary>
/// Stand-in for the inter-service Poll API call so Vote API integration tests don't need
/// the Poll API running. "open12" → active 2-option poll, "closed" → inactive, else null.
/// </summary>
public class FakePollClientService : PollClientService
{
    public FakePollClientService() : base(new HttpClient()) { }

    // "nope1" → not found; "closed" → inactive; any other code → active 2-option poll.
    // Returning a poll for arbitrary codes lets each test use its own code, keeping vote
    // tallies isolated within the shared in-memory database.
    public override Task<PollInfo?> GetPollAsync(string code)
    {
        if (code == "nope1")
            return Task.FromResult<PollInfo?>(null);

        return Task.FromResult<PollInfo?>(new PollInfo
        {
            Code = code,
            Question = "Test?",
            IsActive = code != "closed",
            Options =
            [
                new PollOptionInfo { OptionIndex = 0, Text = "A" },
                new PollOptionInfo { OptionIndex = 1, Text = "B" }
            ]
        });
    }
}

public class CustomWebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var toRemove = services
                .Where(d => d.ServiceType.Name.Contains("DbContextOptions")
                            || d.ServiceType == typeof(PollClientService))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            var dbName = "VoteTest_" + Guid.NewGuid();
            services.AddDbContext<VoteDbContext>(o => o.UseInMemoryDatabase(dbName));
            services.AddSingleton<PollClientService, FakePollClientService>();
        });
    }
}
