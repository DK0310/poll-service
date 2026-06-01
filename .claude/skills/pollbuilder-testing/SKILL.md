---
name: pollbuilder-testing
description: Use when writing unit tests, integration tests, or verifying logic in any Poll Builder microservice. Covers xUnit, Moq, WebApplicationFactory, and per-service test setup.
---

# Poll Builder — Testing Skill

This skill holds **reusable testing patterns** for the microservices: xUnit + Moq unit tests, `WebApplicationFactory` integration tests, the test decision tree, naming convention, and per-method checklist.

> **Project facts live in [ARCHITECTURE.md](../../../ARCHITECTURE.md)** — the test-project layout per service is part of the authoritative folder structure there. This skill does not repeat it.

**Principles:**
- Each service has its own test project; tests run independently (you can test Poll API without Vote API running).
- **Every service method gets at least one success-path test and one failure-path test.**
- Inter-service calls are **mocked** — a unit test never depends on another service being up.
- Unit tests verify service logic in isolation; integration tests verify the full HTTP pipeline of a single service with an in-memory database.

---

## NuGet Packages (per test project)

```bash
# Unit tests
dotnet add package xunit
dotnet add package xunit.runner.visualstudio
dotnet add package Moq
dotnet add package Microsoft.NET.Test.Sdk

# Integration tests (additional)
dotnet add package Microsoft.AspNetCore.Mvc.Testing
dotnet add package Microsoft.EntityFrameworkCore.InMemory
```

---

## Unit Tests — Poll API

### PollServiceTests

```csharp
// services/poll-api/PollApi.Tests/Services/PollServiceTests.cs
using Moq;
using Xunit;

public class PollServiceTests
{
    private readonly Mock<PollRepository> _repoMock;
    private readonly PollService _sut;

    public PollServiceTests()
    {
        // PollRepository needs a DbContext — mock the repository
        _repoMock = new Mock<PollRepository>(MockBehavior.Default, new object[] { null! });
        _sut = new PollService(_repoMock.Object);
    }

    // ── Create Poll: Success ────────────────────────────────

    [Fact]
    public async Task Create_ReturnsSuccess_WhenPollIsValid()
    {
        var request = new CreatePollRequest
        {
            Question = "Favorite color?",
            Options = new List<string> { "Red", "Blue", "Green" }
        };

        _repoMock.Setup(r => r.GetByCodeAsync(It.IsAny<string>()))
            .ReturnsAsync((Poll?)null);

        var result = await _sut.CreateAsync(request, null);

        Assert.True(result.IsSuccess);
        Assert.Equal("Favorite color?", result.Value.Question);
        Assert.Equal(3, result.Value.Options.Count);
    }

    // ── Create Poll: Validation Failures ────────────────────

    [Fact]
    public async Task Create_ReturnsFailure_WhenQuestionEmpty()
    {
        var request = new CreatePollRequest
        {
            Question = "",
            Options = new List<string> { "A", "B" }
        };

        var result = await _sut.CreateAsync(request, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("Question", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_ReturnsFailure_WhenTooFewOptions()
    {
        var request = new CreatePollRequest
        {
            Question = "Test?",
            Options = new List<string> { "Only one" }
        };

        var result = await _sut.CreateAsync(request, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("2 options", result.Error);
    }

    [Fact]
    public async Task Create_ReturnsFailure_WhenTooManyOptions()
    {
        var request = new CreatePollRequest
        {
            Question = "Test?",
            Options = new List<string> { "A", "B", "C", "D", "E", "F", "G" }
        };

        var result = await _sut.CreateAsync(request, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("6 options", result.Error);
    }

    [Fact]
    public async Task Create_ReturnsFailure_WhenOptionIsEmpty()
    {
        var request = new CreatePollRequest
        {
            Question = "Test?",
            Options = new List<string> { "A", "", "C" }
        };

        var result = await _sut.CreateAsync(request, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── Close Poll ──────────────────────────────────────────

    [Fact]
    public async Task Close_ReturnsSuccess_WhenCreator()
    {
        var creatorId = Guid.NewGuid();
        var poll = new Poll
        {
            Code = "abc12",
            Question = "Test?",
            Status = PollStatus.Open,
            CreatorId = creatorId
        };
        _repoMock.Setup(r => r.GetByCodeAsync("abc12")).ReturnsAsync(poll);

        var result = await _sut.CloseAsync("abc12", creatorId);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Close_ReturnsFailure_WhenNotCreator()
    {
        var poll = new Poll
        {
            Code = "abc12",
            CreatorId = Guid.NewGuid()
        };
        _repoMock.Setup(r => r.GetByCodeAsync("abc12")).ReturnsAsync(poll);

        var result = await _sut.CloseAsync("abc12", Guid.NewGuid());

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Close_ReturnsFailure_WhenAlreadyClosed()
    {
        var creatorId = Guid.NewGuid();
        var poll = new Poll
        {
            Code = "abc12",
            Status = PollStatus.Closed,
            CreatorId = creatorId
        };
        _repoMock.Setup(r => r.GetByCodeAsync("abc12")).ReturnsAsync(poll);

        var result = await _sut.CloseAsync("abc12", creatorId);

        Assert.False(result.IsSuccess);
        Assert.Contains("already closed", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── Delete ──────────────────────────────────────────────

    [Fact]
    public async Task Delete_ReturnsSuccess_WhenCreator()
    {
        var creatorId = Guid.NewGuid();
        var poll = new Poll { Code = "del01", CreatorId = creatorId };
        _repoMock.Setup(r => r.GetByCodeAsync("del01")).ReturnsAsync(poll);

        var result = await _sut.DeleteAsync("del01", creatorId);

        Assert.True(result.IsSuccess);
        _repoMock.Verify(r => r.DeleteAsync(poll), Times.Once);
    }

    [Fact]
    public async Task Delete_ReturnsFailure_WhenNotCreator()
    {
        var poll = new Poll { Code = "del01", CreatorId = Guid.NewGuid() };
        _repoMock.Setup(r => r.GetByCodeAsync("del01")).ReturnsAsync(poll);

        var result = await _sut.DeleteAsync("del01", Guid.NewGuid());

        Assert.False(result.IsSuccess);
        _repoMock.Verify(r => r.DeleteAsync(It.IsAny<Poll>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ReturnsFailure_WhenPollNotFound()
    {
        _repoMock.Setup(r => r.GetByCodeAsync("nope")).ReturnsAsync((Poll?)null);

        var result = await _sut.DeleteAsync("nope", Guid.NewGuid());

        Assert.False(result.IsSuccess);
    }
}
```

---

## Unit Tests — Vote API

### VoteServiceTests

The Vote API calls Poll API over HTTP — so we mock `PollClientService`.

```csharp
// services/vote-api/VoteApi.Tests/Services/VoteServiceTests.cs
using Moq;
using Xunit;
using Microsoft.AspNetCore.SignalR;

public class VoteServiceTests
{
    private readonly Mock<VoteRepository> _repoMock;
    private readonly Mock<PollClientService> _pollClientMock;
    private readonly Mock<IHubContext<PollHub>> _hubMock;
    private readonly VoteService _sut;

    public VoteServiceTests()
    {
        _repoMock = new Mock<VoteRepository>(MockBehavior.Default, new object[] { null! });
        _pollClientMock = new Mock<PollClientService>(MockBehavior.Default, new object[] { null! });
        _hubMock = new Mock<IHubContext<PollHub>>();

        // Setup hub mock to return a mock clients proxy
        var mockClients = new Mock<IHubClients>();
        var mockGroup = new Mock<IClientProxy>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockGroup.Object);
        _hubMock.Setup(h => h.Clients).Returns(mockClients.Object);

        _sut = new VoteService(_repoMock.Object, _pollClientMock.Object, _hubMock.Object);
    }

    // ── Submit Vote: Success ────────────────────────────────

    [Fact]
    public async Task SubmitVote_ReturnsSuccess_WhenValid()
    {
        var poll = new PollInfo
        {
            Code = "abc12",
            Question = "Test?",
            IsActive = true,
            Options = new List<PollOptionInfo>
            {
                new() { OptionIndex = 0, Text = "A" },
                new() { OptionIndex = 1, Text = "B" }
            }
        };
        _pollClientMock.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(poll);
        _repoMock.Setup(r => r.HasVotedAsync("abc12", "token123")).ReturnsAsync(false);
        _repoMock.Setup(r => r.GetVoteCountsAsync("abc12"))
            .ReturnsAsync(new List<VoteCount> { new() { OptionIndex = 0, Count = 1 } });

        var request = new VoteRequest { OptionIndex = 0, VoterToken = "token123" };
        var result = await _sut.SubmitVoteAsync("abc12", request);

        Assert.True(result.IsSuccess);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Vote>()), Times.Once);
    }

    // ── Submit Vote: Failures ───────────────────────────────

    [Fact]
    public async Task SubmitVote_ReturnsFailure_WhenPollNotFound()
    {
        _pollClientMock.Setup(c => c.GetPollAsync("nope")).ReturnsAsync((PollInfo?)null);

        var request = new VoteRequest { OptionIndex = 0, VoterToken = "token" };
        var result = await _sut.SubmitVoteAsync("nope", request);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitVote_ReturnsFailure_WhenPollClosed()
    {
        var poll = new PollInfo { Code = "cls01", IsActive = false, Options = new() };
        _pollClientMock.Setup(c => c.GetPollAsync("cls01")).ReturnsAsync(poll);

        var request = new VoteRequest { OptionIndex = 0, VoterToken = "token" };
        var result = await _sut.SubmitVoteAsync("cls01", request);

        Assert.False(result.IsSuccess);
        Assert.Contains("closed", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitVote_ReturnsFailure_WhenInvalidOption()
    {
        var poll = new PollInfo
        {
            Code = "abc12",
            IsActive = true,
            Options = new List<PollOptionInfo>
            {
                new() { OptionIndex = 0, Text = "A" },
                new() { OptionIndex = 1, Text = "B" }
            }
        };
        _pollClientMock.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(poll);

        var request = new VoteRequest { OptionIndex = 5, VoterToken = "token" };
        var result = await _sut.SubmitVoteAsync("abc12", request);

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid option", result.Error);
    }

    [Fact]
    public async Task SubmitVote_ReturnsFailure_WhenDuplicateVote()
    {
        var poll = new PollInfo
        {
            Code = "abc12",
            IsActive = true,
            Options = new List<PollOptionInfo> { new() { OptionIndex = 0, Text = "A" } }
        };
        _pollClientMock.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(poll);
        _repoMock.Setup(r => r.HasVotedAsync("abc12", "dup-token")).ReturnsAsync(true);

        var request = new VoteRequest { OptionIndex = 0, VoterToken = "dup-token" };
        var result = await _sut.SubmitVoteAsync("abc12", request);

        Assert.False(result.IsSuccess);
        Assert.Contains("already voted", result.Error, StringComparison.OrdinalIgnoreCase);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Vote>()), Times.Never);
    }

    [Fact]
    public async Task SubmitVote_ReturnsFailure_WhenVoterTokenEmpty()
    {
        var poll = new PollInfo
        {
            Code = "abc12",
            IsActive = true,
            Options = new List<PollOptionInfo> { new() { OptionIndex = 0, Text = "A" } }
        };
        _pollClientMock.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(poll);

        var request = new VoteRequest { OptionIndex = 0, VoterToken = "" };
        var result = await _sut.SubmitVoteAsync("abc12", request);

        Assert.False(result.IsSuccess);
    }
}
```

---

## Integration Tests

### CustomWebApplicationFactory (per service)

Each service has its own factory that swaps the real database for in-memory:

```csharp
// services/poll-api/PollApi.Tests/Integration/CustomWebAppFactory.cs
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class CustomWebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove real DB
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<PollDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            // Add in-memory DB
            services.AddDbContext<PollDbContext>(options =>
                options.UseInMemoryDatabase("TestPollDb_" + Guid.NewGuid()));

            // Ensure DB is created
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PollDbContext>();
            db.Database.EnsureCreated();
        });

        builder.UseEnvironment("Testing");
    }
}
```

> **Make `Program` accessible:** Add `public partial class Program { }` at the bottom of each service's `Program.cs`.

### Poll API Integration Tests

```csharp
// services/poll-api/PollApi.Tests/Integration/PollEndpointTests.cs
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

public class PollEndpointTests : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _client;

    public PollEndpointTests(CustomWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreatePoll_Returns201_WhenValid()
    {
        var request = new
        {
            question = "Favorite language?",
            options = new[] { "C#", "JavaScript", "Python" }
        };

        var response = await _client.PostAsJsonAsync("/api/polls", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Favorite language?", body.GetProperty("question").GetString());
        Assert.Equal(5, body.GetProperty("code").GetString()!.Length);
    }

    [Fact]
    public async Task CreatePoll_Returns400_WhenQuestionEmpty()
    {
        var request = new { question = "", options = new[] { "A", "B" } };

        var response = await _client.PostAsJsonAsync("/api/polls", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPoll_Returns200_WhenExists()
    {
        // Create first
        var createReq = new { question = "Test?", options = new[] { "A", "B" } };
        var createRes = await _client.PostAsJsonAsync("/api/polls", createReq);
        var body = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        var code = body.GetProperty("code").GetString();

        // Get
        var response = await _client.GetAsync($"/api/polls/{code}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetPoll_Returns404_WhenNotExists()
    {
        var response = await _client.GetAsync("/api/polls/ZZZZZ");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
```

---

## Running Tests

### Commands

```bash
# Run all tests for a specific service
dotnet test services/poll-api/PollApi.sln
dotnet test services/vote-api/VoteApi.sln
dotnet test services/identity-api/IdentityApi.sln

# Run all services' tests
dotnet test services/poll-api/PollApi.sln
dotnet test services/vote-api/VoteApi.sln
dotnet test services/identity-api/IdentityApi.sln

# Run a specific test
dotnet test --filter "Create_ReturnsSuccess_WhenPollIsValid"

# Verbose output
dotnet test --logger "console;verbosity=detailed"

# CI/CD format
dotnet test --no-build --logger trx
```

### Inside Docker

```bash
docker-compose exec poll-api dotnet test
docker-compose exec vote-api dotnet test
docker-compose exec identity-api dotnet test
```

---

## Test Decision Tree

```
WHAT TYPE OF TEST DO I NEED?

├─ Testing service logic in isolation?
│  └─ UNIT TEST (xUnit + Moq)
│     Mock repository, mock PollClientService
│     Verify Result<T>.IsSuccess / .Error
│     Verify mock interactions

├─ Testing HTTP endpoint end-to-end (single service)?
│  └─ INTEGRATION TEST (WebApplicationFactory)
│     In-memory database, real DI container
│     Send HTTP request, check status + body

├─ Testing inter-service communication?
│  └─ UNIT TEST with mocked PollClientService
│     Mock the HTTP client response
│     Verify the service handles success/failure correctly

├─ Testing SignalR broadcast?
│  └─ UNIT TEST with mocked IHubContext
│     Verify Clients.Group(code).SendAsync was called

├─ Testing auth enforcement?
│  └─ Test at Gateway level (or manually with Postman)
│     Verify protected routes return 401 without token

└─ Testing the full system (all services)?
   └─ Manual test with docker-compose up
      Or E2E test framework (Playwright, Cypress)
```

---

## Test Naming Convention

```
{MethodName}_{ExpectedResult}_{Condition}

Examples:
  Create_ReturnsSuccess_WhenPollIsValid
  Create_ReturnsFailure_WhenQuestionEmpty
  Create_ReturnsFailure_WhenTooFewOptions
  Create_ReturnsFailure_WhenTooManyOptions
  Close_ReturnsFailure_WhenNotCreator
  Close_ReturnsFailure_WhenAlreadyClosed
  SubmitVote_ReturnsFailure_WhenDuplicateVote
  SubmitVote_ReturnsFailure_WhenPollClosed
  SubmitVote_ReturnsFailure_WhenInvalidOption
```

---

## Test Checklist (per service method)

```
□ Success path returns Result<T>.Success with correct data
□ Each validation failure returns correct error message
□ Permission denied returns failure (not exception)
□ Not-found returns failure
□ Side effects (DB write, broadcast) happen on success
□ Side effects DON'T happen when validation fails
□ Inter-service failures handled gracefully (Poll API down)
□ Error messages are user-friendly, not technical
```

---

## Test Project Files

### PollApi.Tests.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="Moq" Version="4.*" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.*" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\PollApi\PollApi.csproj" />
  </ItemGroup>
</Project>
```

### VoteApi.Tests.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="Moq" Version="4.*" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.*" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\VoteApi\VoteApi.csproj" />
  </ItemGroup>
</Project>
```

---

## Common Mistakes

| ❌ Don't | ✅ Do | Why |
|---|---|---|
| Test with real database | Use `UseInMemoryDatabase` | Tests must be fast and isolated |
| Test inter-service calls with real HTTP | Mock `PollClientService` | Unit tests don't depend on other services |
| Share database between test classes | Use `Guid.NewGuid()` in DB name | Prevents test pollution |
| Skip testing failure paths | Test every `Result<T>.Failure` branch | Failures matter as much as successes |
| Test controller logic directly | Test through service (unit) or HTTP (integration) | Controllers should have no logic |
| Assert only status code | Assert response body too | Verify the full contract |
| Skip SignalR broadcast verification | Mock `IHubContext` and verify `SendAsync` | Ensure broadcasts happen |
| Write tests after all code is done | Write tests alongside each feature | Catch bugs earlier |

---

## Cross-References

- **Authoritative project & test-project structure** → [ARCHITECTURE.md](../../../ARCHITECTURE.md)
- **Service implementations under test** → `pollbuilder-backend`
- **System design principles** → `pollbuilder-architecture`
- **Database per service** → `pollbuilder-database`
- **CI/CD test step** → `pollbuilder-devops`
- **Frontend** → `pollbuilder-frontend`
