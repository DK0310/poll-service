---
name: pollbuilder-backend
description: Use when implementing any microservice endpoint, service, repository, DTO, SignalR Hub, or inter-service communication in ASP.NET Core 8
---

# Poll Builder — Backend Patterns Skill

This skill holds **reusable ASP.NET Core 8 implementation patterns** for the backend services. The concrete examples use this project's classes so they drop straight in, but the patterns (layering, `Result<T>`, typed clients, broadcasting, middleware) transfer to any service.

> **Project facts live in [ARCHITECTURE.md](../../../ARCHITECTURE.md)** — the service map and ports, per-service folder layout, endpoint list, Gateway routing table, and environment variables. This skill does not repeat them.

## The Layered Pattern (every service)

Each service is an independent ASP.NET Core 8 project. Every request flows through the same layers:

```
Controller  → parse request, return status code (thin, no business logic)
   Service  → business rules + validation, returns Result<T> (never throws for expected failures)
Repository  → the only layer that touches the DbContext
```

Cross-cutting collaborators: a typed **client service** for inter-service HTTP calls (Vote → Poll), a **SignalR Hub** + `IHubContext` broadcast for real-time (Vote API), and **error-handling middleware** in every service. See [ARCHITECTURE.md](../../../ARCHITECTURE.md) for which service has which.

---

## Result\<T\> Pattern

**Used in every service.** Each service has its own `Result<T>` class (or a shared project).

```csharp
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }

    private Result(T value) { IsSuccess = true; Value = value; }
    private Result(string error) { IsSuccess = false; Error = error; }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string error) => new(error);
}
```

---

## Poll API — Complete Implementation

### Controller

```csharp
// services/poll-api/PollApi/Controllers/PollsController.cs
[ApiController]
[Route("api/[controller]")]
public class PollsController : ControllerBase
{
    private readonly PollService _service;
    public PollsController(PollService service) => _service = service;

    // ── POST /api/polls ─────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePollRequest request)
    {
        // CreatorId comes from X-User-Id header (set by Gateway after JWT validation)
        Guid? creatorId = Request.Headers.TryGetValue("X-User-Id", out var uid)
            ? Guid.TryParse(uid, out var id) ? id : null
            : null;

        var result = await _service.CreateAsync(request, creatorId);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetPoll), new { code = result.Value.Code }, result.Value)
            : BadRequest(new { error = result.Error });
    }

    // ── GET /api/polls/{code} ───────────────────────────────────
    [HttpGet("{code}")]
    public async Task<IActionResult> GetPoll(string code)
    {
        var result = await _service.GetByCodeAsync(code);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    // ── PATCH /api/polls/{code}/close ───────────────────────────
    [HttpPatch("{code}/close")]
    public async Task<IActionResult> Close(string code)
    {
        // Creator verification via X-User-Id header
        if (!Request.Headers.TryGetValue("X-User-Id", out var uid)
            || !Guid.TryParse(uid, out var userId))
            return Unauthorized();

        var result = await _service.CloseAsync(code, userId);
        return result.IsSuccess ? Ok(result.Value) : Forbid();
    }

    // ── DELETE /api/polls/{code} ────────────────────────────────
    [HttpDelete("{code}")]
    public async Task<IActionResult> Delete(string code)
    {
        if (!Request.Headers.TryGetValue("X-User-Id", out var uid)
            || !Guid.TryParse(uid, out var userId))
            return Unauthorized();

        var result = await _service.DeleteAsync(code, userId);
        return result.IsSuccess ? NoContent() : Forbid();
    }

    // ── GET /api/polls/my-polls ─────────────────────────────────
    [HttpGet("my-polls")]
    public async Task<IActionResult> MyPolls()
    {
        if (!Request.Headers.TryGetValue("X-User-Id", out var uid)
            || !Guid.TryParse(uid, out var userId))
            return Unauthorized();

        var result = await _service.GetByCreatorAsync(userId);
        return Ok(result);
    }
}
```

### Service

```csharp
// services/poll-api/PollApi/Services/PollService.cs
public class PollService
{
    private readonly PollRepository _repo;
    public PollService(PollRepository repo) => _repo = repo;

    public async Task<Result<PollResponse>> CreateAsync(CreatePollRequest request, Guid? creatorId)
    {
        // Validate
        if (string.IsNullOrWhiteSpace(request.Question))
            return Result<PollResponse>.Failure("Question is required");

        if (request.Options == null || request.Options.Count < 2)
            return Result<PollResponse>.Failure("At least 2 options are required");

        if (request.Options.Count > 6)
            return Result<PollResponse>.Failure("Maximum 6 options allowed");

        if (request.Options.Any(o => string.IsNullOrWhiteSpace(o)))
            return Result<PollResponse>.Failure("Option text cannot be empty");

        // Generate unique code
        var code = await GenerateUniqueCodeAsync();

        // Build entity
        var poll = new Poll
        {
            Code = code,
            Question = request.Question,
            Status = PollStatus.Open,
            CreatorId = creatorId,
            ExpiresAt = request.ExpiryHours.HasValue
                ? DateTime.UtcNow.AddHours(request.ExpiryHours.Value)
                : null,
            Options = request.Options.Select((text, i) => new PollOption
            {
                OptionIndex = i,
                Text = text
            }).ToList()
        };

        await _repo.AddAsync(poll);
        return Result<PollResponse>.Success(PollResponse.From(poll));
    }

    public async Task<Result<PollResponse>> GetByCodeAsync(string code)
    {
        var poll = await _repo.GetByCodeAsync(code);
        if (poll is null)
            return Result<PollResponse>.Failure("Poll not found");
        return Result<PollResponse>.Success(PollResponse.From(poll));
    }

    public async Task<Result<PollResponse>> CloseAsync(string code, Guid userId)
    {
        var poll = await _repo.GetByCodeAsync(code);
        if (poll is null) return Result<PollResponse>.Failure("Poll not found");
        if (poll.CreatorId != userId) return Result<PollResponse>.Failure("Not the poll creator");
        if (poll.IsClosed) return Result<PollResponse>.Failure("Poll is already closed");

        poll.Status = PollStatus.Closed;
        await _repo.UpdateAsync(poll);
        return Result<PollResponse>.Success(PollResponse.From(poll));
    }

    public async Task<Result<bool>> DeleteAsync(string code, Guid userId)
    {
        var poll = await _repo.GetByCodeAsync(code);
        if (poll is null) return Result<bool>.Failure("Poll not found");
        if (poll.CreatorId != userId) return Result<bool>.Failure("Not the poll creator");

        await _repo.DeleteAsync(poll);
        return Result<bool>.Success(true);
    }

    public async Task<IEnumerable<PollResponse>> GetByCreatorAsync(Guid userId)
    {
        var polls = await _repo.GetByCreatorAsync(userId);
        return polls.Select(PollResponse.From);
    }

    private async Task<string> GenerateUniqueCodeAsync()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string code;
        do
        {
            code = new string(Enumerable.Range(0, 5)
                .Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)])
                .ToArray());
        }
        while (await _repo.GetByCodeAsync(code) is not null);
        return code;
    }
}
```

### Repository

```csharp
// services/poll-api/PollApi/Repositories/PollRepository.cs
public class PollRepository
{
    private readonly PollDbContext _db;
    public PollRepository(PollDbContext db) => _db = db;

    public Task<Poll?> GetByCodeAsync(string code)
        => _db.Polls
            .Include(p => p.Options.OrderBy(o => o.OptionIndex))
            .FirstOrDefaultAsync(p => p.Code == code);

    public async Task AddAsync(Poll poll)
    {
        _db.Polls.Add(poll);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Poll poll)
    {
        _db.Polls.Update(poll);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Poll poll)
    {
        _db.Polls.Remove(poll);
        await _db.SaveChangesAsync();
    }

    public async Task<IEnumerable<Poll>> GetByCreatorAsync(Guid userId, int limit = 50)
        => await _db.Polls
            .Include(p => p.Options)
            .Where(p => p.CreatorId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync();

    public async Task<IEnumerable<Poll>> GetExpiredAsync()
        => await _db.Polls
            .Where(p => p.Status == PollStatus.Open
                     && p.ExpiresAt != null
                     && p.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();
}
```

### DTOs

```csharp
public record CreatePollRequest
{
    [Required] public string Question { get; init; } = "";
    [Required] public List<string> Options { get; init; } = new();
    public int? ExpiryHours { get; init; }
}

public record PollResponse
{
    public string Code { get; init; } = "";
    public string Question { get; init; } = "";
    public string Status { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public bool IsActive { get; init; }
    public List<OptionResponse> Options { get; init; } = new();
    public string Url => $"/poll/{Code}";

    public static PollResponse From(Poll e) => new()
    {
        Code = e.Code,
        Question = e.Question,
        Status = e.Status.ToString(),
        CreatedAt = e.CreatedAt,
        ExpiresAt = e.ExpiresAt,
        IsActive = e.IsActive,
        Options = e.Options.OrderBy(o => o.OptionIndex)
            .Select(o => new OptionResponse { OptionIndex = o.OptionIndex, Text = o.Text })
            .ToList()
    };
}

public record OptionResponse
{
    public int OptionIndex { get; init; }
    public string Text { get; init; } = "";
}
```

### Program.cs (Poll API)

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PollDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<PollRepository>();
builder.Services.AddScoped<PollService>();
builder.Services.AddHostedService<PollCleanupService>();
builder.Services.AddControllers();

var app = builder.Build();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.MapControllers();
app.Run();
```

> **Note:** No CORS, no JWT validation — the Gateway handles both. Poll API is only reachable internally.

---

## Vote API — Complete Implementation

### Controller

```csharp
// services/vote-api/VoteApi/Controllers/VotesController.cs
[ApiController]
[Route("api/polls")]
public class VotesController : ControllerBase
{
    private readonly VoteService _service;
    public VotesController(VoteService service) => _service = service;

    // ── POST /api/polls/{code}/vote ─────────────────────────────
    [HttpPost("{code}/vote")]
    public async Task<IActionResult> Vote(string code, [FromBody] VoteRequest request)
    {
        var result = await _service.SubmitVoteAsync(code, request);
        if (result.IsSuccess) return Ok(result.Value);
        if (result.Error!.Contains("already voted")) return Conflict(new { error = result.Error });
        return BadRequest(new { error = result.Error });
    }

    // ── GET /api/polls/{code}/results ───────────────────────────
    [HttpGet("{code}/results")]
    public async Task<IActionResult> Results(string code)
    {
        var result = await _service.GetResultsAsync(code);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }
}
```

### VoteService (with inter-service call + SignalR broadcast)

```csharp
// services/vote-api/VoteApi/Services/VoteService.cs
public class VoteService
{
    private readonly VoteRepository _repo;
    private readonly PollClientService _pollClient;
    private readonly IHubContext<PollHub> _hubContext;

    public VoteService(VoteRepository repo, PollClientService pollClient, IHubContext<PollHub> hubContext)
    {
        _repo = repo;
        _pollClient = pollClient;
        _hubContext = hubContext;
    }

    public async Task<Result<VoteResultsResponse>> SubmitVoteAsync(string code, VoteRequest request)
    {
        // STEP 1: Validate poll exists and is active (call Poll API)
        var poll = await _pollClient.GetPollAsync(code);
        if (poll is null)
            return Result<VoteResultsResponse>.Failure("Poll not found");
        if (!poll.IsActive)
            return Result<VoteResultsResponse>.Failure("Poll is closed or expired");

        // STEP 2: Validate option index
        if (request.OptionIndex < 0 || request.OptionIndex >= poll.Options.Count)
            return Result<VoteResultsResponse>.Failure("Invalid option selected");

        // STEP 3: Check duplicate vote
        if (string.IsNullOrWhiteSpace(request.VoterToken))
            return Result<VoteResultsResponse>.Failure("Voter token is required");
        if (await _repo.HasVotedAsync(code, request.VoterToken))
            return Result<VoteResultsResponse>.Failure("You have already voted on this poll");

        // STEP 4: Save vote
        var vote = new Vote
        {
            PollCode = code,
            OptionIndex = request.OptionIndex,
            VoterToken = request.VoterToken,
            VotedAt = DateTime.UtcNow
        };
        await _repo.AddAsync(vote);

        // STEP 5: Build updated results
        var results = await BuildResultsAsync(code, poll);

        // STEP 6: Broadcast to all clients watching this poll
        await _hubContext.Clients.Group(code)
            .SendAsync("ReceiveVoteUpdate", results);

        return Result<VoteResultsResponse>.Success(results);
    }

    public async Task<Result<VoteResultsResponse>> GetResultsAsync(string code)
    {
        var poll = await _pollClient.GetPollAsync(code);
        if (poll is null)
            return Result<VoteResultsResponse>.Failure("Poll not found");

        var results = await BuildResultsAsync(code, poll);
        return Result<VoteResultsResponse>.Success(results);
    }

    private async Task<VoteResultsResponse> BuildResultsAsync(string code, PollInfo poll)
    {
        var voteCounts = await _repo.GetVoteCountsAsync(code);
        var totalVotes = voteCounts.Sum(vc => vc.Count);

        return new VoteResultsResponse
        {
            PollCode = code,
            Question = poll.Question,
            TotalVotes = totalVotes,
            IsActive = poll.IsActive,
            Options = poll.Options
                .OrderBy(o => o.OptionIndex)
                .Select(o =>
                {
                    var count = voteCounts.FirstOrDefault(vc => vc.OptionIndex == o.OptionIndex)?.Count ?? 0;
                    return new OptionResult
                    {
                        OptionIndex = o.OptionIndex,
                        Text = o.Text,
                        VoteCount = count,
                        Percentage = totalVotes > 0 ? Math.Round((double)count / totalVotes * 100, 1) : 0
                    };
                }).ToList()
        };
    }
}
```

### PollClientService (inter-service HTTP client)

```csharp
// services/vote-api/VoteApi/Services/PollClientService.cs
public class PollClientService
{
    private readonly HttpClient _http;

    public PollClientService(HttpClient http) => _http = http;

    public async Task<PollInfo?> GetPollAsync(string code)
    {
        try
        {
            var response = await _http.GetAsync($"/api/polls/{code}");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<PollInfo>();
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}

// DTO for poll data received from Poll API
public record PollInfo
{
    public string Code { get; init; } = "";
    public string Question { get; init; } = "";
    public bool IsActive { get; init; }
    public List<PollOptionInfo> Options { get; init; } = new();
}

public record PollOptionInfo
{
    public int OptionIndex { get; init; }
    public string Text { get; init; } = "";
}
```

### SignalR Hub

```csharp
// services/vote-api/VoteApi/Hubs/PollHub.cs
public class PollHub : Hub
{
    public async Task JoinPollGroup(string pollCode)
        => await Groups.AddToGroupAsync(Context.ConnectionId, pollCode);

    public async Task LeavePollGroup(string pollCode)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, pollCode);
}
```

### VoteRepository

```csharp
// services/vote-api/VoteApi/Repositories/VoteRepository.cs
public class VoteRepository
{
    private readonly VoteDbContext _db;
    public VoteRepository(VoteDbContext db) => _db = db;

    public async Task AddAsync(Vote vote)
    {
        _db.Votes.Add(vote);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> HasVotedAsync(string pollCode, string voterToken)
        => await _db.Votes.AnyAsync(v => v.PollCode == pollCode && v.VoterToken == voterToken);

    public async Task<List<VoteCount>> GetVoteCountsAsync(string pollCode)
        => await _db.Votes
            .Where(v => v.PollCode == pollCode)
            .GroupBy(v => v.OptionIndex)
            .Select(g => new VoteCount { OptionIndex = g.Key, Count = g.Count() })
            .ToListAsync();
}

public class VoteCount
{
    public int OptionIndex { get; set; }
    public int Count { get; set; }
}
```

### DTOs (Vote API)

```csharp
public record VoteRequest
{
    [Required] public int OptionIndex { get; init; }
    [Required] public string VoterToken { get; init; } = "";
}

public record VoteResultsResponse
{
    public string PollCode { get; init; } = "";
    public string Question { get; init; } = "";
    public int TotalVotes { get; init; }
    public bool IsActive { get; init; }
    public List<OptionResult> Options { get; init; } = new();
}

public record OptionResult
{
    public int OptionIndex { get; init; }
    public string Text { get; init; } = "";
    public int VoteCount { get; init; }
    public double Percentage { get; init; }
}
```

### Program.cs (Vote API)

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<VoteDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<VoteRepository>();
builder.Services.AddScoped<VoteService>();
builder.Services.AddSignalR();

// Register typed HttpClient for inter-service communication
builder.Services.AddHttpClient<PollClientService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:PollApi"]!);
    // docker-compose: http://poll-api:8080
});

// CORS needed for SignalR (WebSocket requires credentials)
builder.Services.AddCors(opt => opt.AddPolicy("SignalR", p =>
    p.WithOrigins(builder.Configuration["Gateway:Url"]!)
     .AllowAnyMethod().AllowAnyHeader().AllowCredentials()));

builder.Services.AddControllers();

var app = builder.Build();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseCors("SignalR");
app.MapControllers();
app.MapHub<PollHub>("/hubs/poll");
app.Run();
```

---

## Identity API — Complete Implementation

### Controller

```csharp
// services/identity-api/IdentityApi/Controllers/AuthController.cs
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _service;
    public AuthController(AuthService service) => _service = service;

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _service.RegisterAsync(request);
        return result.IsSuccess ? Ok(new { token = result.Value }) : BadRequest(new { error = result.Error });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _service.LoginAsync(request);
        return result.IsSuccess ? Ok(new { token = result.Value }) : BadRequest(new { error = result.Error });
    }
}
```

### AuthService

```csharp
// services/identity-api/IdentityApi/Services/AuthService.cs
public class AuthService
{
    private readonly IdentityDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(IdentityDbContext db, IConfiguration config)
    { _db = db; _config = config; }

    public async Task<Result<string>> RegisterAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return Result<string>.Failure("Email is required");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return Result<string>.Failure("Password must be at least 6 characters");

        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            return Result<string>.Failure("Email already registered");

        var user = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Result<string>.Success(GenerateToken(user));
    }

    public async Task<Result<string>> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Result<string>.Failure("Invalid email or password");

        return Result<string>.Success(GenerateToken(user));
    }

    private string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())],
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

### Program.cs (Identity API)

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<IdentityDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<AuthService>();
builder.Services.AddControllers();

var app = builder.Build();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.MapControllers();
app.Run();
```

---

## API Gateway — YARP Implementation

### Program.cs

```csharp
// services/gateway/Gateway/Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// JWT validation at gateway (centralized)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

builder.Services.AddAuthorization(opt =>
{
    opt.AddPolicy("authenticated", p => p.RequireAuthenticatedUser());
});

builder.Services.AddCors(opt => opt.AddPolicy("Frontend", p =>
    p.WithOrigins(builder.Configuration["Frontend:Url"]!)
     .AllowAnyMethod().AllowAnyHeader().AllowCredentials()));

var app = builder.Build();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();
app.Run();
```

### Route Configuration (appsettings.json)

The full route + cluster table (orders, methods, transforms, cluster addresses) is in **[ARCHITECTURE.md → Gateway Routing Table](../../../ARCHITECTURE.md)**. The reusable patterns to apply when editing it:

- **A route per concern**, each pointing at a cluster (`vote-api`, `poll-api`, `identity-api`), each cluster mapping to a Docker service address.
- **Order matters.** Specific paths (`/vote`, `/results`, `/hubs`, `/my-polls`) get low `Order` values; the catch-all (`/api/polls/{**remainder}`) gets a high one so it never shadows them.
- **Protected routes** carry `"AuthorizationPolicy": "authenticated"` plus a transform that copies the JWT `nameidentifier` claim into an `X-User-Id` request header:

```json
"Transforms": [
  { "RequestHeader": "X-User-Id", "Set": "{claim:http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier}" }
]
```

**Key design decision:** the Gateway extracts the user ID from the JWT and forwards it as `X-User-Id`. Downstream services read that header instead of validating the JWT themselves.

---

## Error Handling Middleware

**Every service** has this middleware:

```csharp
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    { _next = next; _logger = logger; }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await _next(ctx); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in {Service}", ctx.Request.Path);
            ctx.Response.StatusCode = 500;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred" });
        }
    }
}
```

---

## Background Cleanup Service (Poll API)

```csharp
// services/poll-api/PollApi/Services/PollCleanupService.cs
public class PollCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PollCleanupService> _logger;

    public PollCleanupService(IServiceScopeFactory sf, ILogger<PollCleanupService> log)
    { _scopeFactory = sf; _logger = log; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(1), ct);
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<PollRepository>();

            var expired = await repo.GetExpiredAsync();
            foreach (var poll in expired)
            {
                poll.Status = PollStatus.Closed;
                await repo.UpdateAsync(poll);
                _logger.LogInformation("Auto-closed expired poll {Code}", poll.Code);
            }
        }
    }
}
```

---

## Common Mistakes

| ❌ Don't | ✅ Do | Why |
|---|---|---|
| Share DbContext between services | Each service has its own DbContext/Database | Data ownership |
| Call Poll API database from Vote API | Use `PollClientService` (HTTP call) | Service boundary |
| Validate JWT in every service | Validate at Gateway, forward X-User-Id | Centralized auth |
| Put business logic in controllers | Put in service classes | Testability |
| Throw exceptions for validation | Return `Result<T>.Failure()` | Explicit control flow |
| Use `new HttpClient()` | Use `AddHttpClient<T>()` DI | Socket management |
| Route service-to-service via Gateway | Call directly: `http://poll-api:8080` | Gateway is for external traffic |
| Return entity from controller | Return DTO via `.From(entity)` | Never expose internals |
| Skip `.AllowCredentials()` for SignalR | Always include in CORS | WebSocket requirement |

---

## Cross-References

- **Project structure, endpoints, Gateway routing, env vars** → [ARCHITECTURE.md](../../../ARCHITECTURE.md)
- **System design principles & decision trees** → `pollbuilder-architecture`
- **Schema, DbContext config, migrations, queries** → `pollbuilder-database`
- **Frontend integration** → `pollbuilder-frontend`
- **Testing services & endpoints** → `pollbuilder-testing`
- **Docker, CI/CD, deployment** → `pollbuilder-devops`
