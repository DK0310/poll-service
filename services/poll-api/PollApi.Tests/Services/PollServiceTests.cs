using Moq;
using PollApi.DTOs;
using PollApi.Models;
using PollApi.Repositories;
using PollApi.Services;

namespace PollApi.Tests.Services;

public class PollServiceTests
{
    private readonly Mock<PollRepository> _repo;
    private readonly PollService _sut;

    public PollServiceTests()
    {
        // PollRepository needs a DbContext; pass null since every used method is mocked (virtual).
        _repo = new Mock<PollRepository>(MockBehavior.Loose, new object[] { null! });
        _sut = new PollService(_repo.Object);
    }

    private static CreatePollRequest Request(string question, params string[] options) =>
        new() { Question = question, Options = options.ToList() };

    // ── Create: success ─────────────────────────────────────────

    [Fact]
    public async Task Create_ReturnsSuccess_WhenValid()
    {
        _repo.Setup(r => r.GetByCodeAsync(It.IsAny<string>())).ReturnsAsync((Poll?)null);

        var result = await _sut.CreateAsync(Request("Favorite color?", "Red", "Blue", "Green"), null);

        Assert.True(result.IsSuccess);
        Assert.Equal("Favorite color?", result.Value!.Question);
        Assert.Equal(3, result.Value.Options.Count);
        Assert.Equal(5, result.Value.Code.Length);
        Assert.Equal("Open", result.Value.Status);
        Assert.True(result.Value.IsActive);
        _repo.Verify(r => r.AddAsync(It.IsAny<Poll>()), Times.Once);
    }

    [Fact]
    public async Task Create_SetsExpiry_WhenExpiryHoursProvided()
    {
        _repo.Setup(r => r.GetByCodeAsync(It.IsAny<string>())).ReturnsAsync((Poll?)null);
        var request = new CreatePollRequest
        {
            Question = "Lunch?",
            Options = new() { "Pizza", "Sushi" },
            ExpiryHours = 24
        };

        var result = await _sut.CreateAsync(request, null);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.ExpiresAt);
    }

    // ── Create: validation failures ─────────────────────────────

    [Fact]
    public async Task Create_ReturnsFailure_WhenQuestionEmpty()
    {
        var result = await _sut.CreateAsync(Request("", "A", "B"), null);

        Assert.False(result.IsSuccess);
        Assert.Contains("Question", result.Error!, StringComparison.OrdinalIgnoreCase);
        _repo.Verify(r => r.AddAsync(It.IsAny<Poll>()), Times.Never);
    }

    [Fact]
    public async Task Create_ReturnsFailure_WhenTooFewOptions()
    {
        var result = await _sut.CreateAsync(Request("Test?", "Only one"), null);

        Assert.False(result.IsSuccess);
        Assert.Contains("2 options", result.Error!);
    }

    [Fact]
    public async Task Create_ReturnsFailure_WhenTooManyOptions()
    {
        var result = await _sut.CreateAsync(Request("Test?", "A", "B", "C", "D", "E", "F", "G"), null);

        Assert.False(result.IsSuccess);
        Assert.Contains("6 options", result.Error!);
    }

    [Fact]
    public async Task Create_ReturnsFailure_WhenOptionIsEmpty()
    {
        var result = await _sut.CreateAsync(Request("Test?", "A", "", "C"), null);

        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    // ── Create: question types (Merit) ──────────────────────────

    [Fact]
    public async Task Create_YesNo_GeneratesYesNoOptions()
    {
        _repo.Setup(r => r.GetByCodeAsync(It.IsAny<string>())).ReturnsAsync((Poll?)null);

        var result = await _sut.CreateAsync(new CreatePollRequest { Question = "Agree?", Type = "YesNo" }, null);

        Assert.True(result.IsSuccess);
        Assert.Equal("YesNo", result.Value!.Type);
        Assert.Equal(new[] { "Yes", "No" }, result.Value.Options.Select(o => o.Text).ToArray());
    }

    [Fact]
    public async Task Create_Rating_GeneratesFiveOptions()
    {
        _repo.Setup(r => r.GetByCodeAsync(It.IsAny<string>())).ReturnsAsync((Poll?)null);

        var result = await _sut.CreateAsync(new CreatePollRequest { Question = "Rate it", Type = "Rating" }, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value!.Options.Count);
        Assert.Equal("5", result.Value.Options[4].Text);
    }

    [Fact]
    public async Task Create_OpenText_HasNoOptions()
    {
        _repo.Setup(r => r.GetByCodeAsync(It.IsAny<string>())).ReturnsAsync((Poll?)null);

        var result = await _sut.CreateAsync(new CreatePollRequest { Question = "Thoughts?", Type = "OpenText" }, null);

        Assert.True(result.IsSuccess);
        Assert.Equal("OpenText", result.Value!.Type);
        Assert.Empty(result.Value.Options);
    }

    [Fact]
    public async Task Create_ReturnsFailure_WhenInvalidType()
    {
        var result = await _sut.CreateAsync(
            new CreatePollRequest { Question = "Q", Type = "Bogus", Options = new() { "a", "b" } }, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("type", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    // ── GetByCode ───────────────────────────────────────────────

    [Fact]
    public async Task GetByCode_ReturnsSuccess_WhenFound()
    {
        var poll = new Poll
        {
            Code = "abc12",
            Question = "Test?",
            Options = new List<PollOption>
            {
                new() { OptionIndex = 1, Text = "B" },
                new() { OptionIndex = 0, Text = "A" }
            }
        };
        _repo.Setup(r => r.GetByCodeAsync("abc12")).ReturnsAsync(poll);

        var result = await _sut.GetByCodeAsync("abc12");

        Assert.True(result.IsSuccess);
        Assert.Equal("Test?", result.Value!.Question);
        // Options come back ordered by index
        Assert.Equal(0, result.Value.Options[0].OptionIndex);
        Assert.Equal(1, result.Value.Options[1].OptionIndex);
    }

    [Fact]
    public async Task GetByCode_ReturnsFailure_WhenNotFound()
    {
        _repo.Setup(r => r.GetByCodeAsync("nope1")).ReturnsAsync((Poll?)null);

        var result = await _sut.GetByCodeAsync("nope1");

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    // ── Close (creator-only; logic ready for Phase 6) ───────────

    [Fact]
    public async Task Close_ReturnsSuccess_WhenCreator()
    {
        var creatorId = Guid.NewGuid();
        var poll = new Poll { Code = "abc12", Question = "Q?", Status = PollStatus.Open, CreatorId = creatorId };
        _repo.Setup(r => r.GetByCodeAsync("abc12")).ReturnsAsync(poll);

        var result = await _sut.CloseAsync("abc12", creatorId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Closed", result.Value!.Status);
        _repo.Verify(r => r.UpdateAsync(poll), Times.Once);
    }

    [Fact]
    public async Task Close_ReturnsFailure_WhenNotCreator()
    {
        var poll = new Poll { Code = "abc12", CreatorId = Guid.NewGuid() };
        _repo.Setup(r => r.GetByCodeAsync("abc12")).ReturnsAsync(poll);

        var result = await _sut.CloseAsync("abc12", Guid.NewGuid());

        Assert.False(result.IsSuccess);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<Poll>()), Times.Never);
    }

    [Fact]
    public async Task Close_ReturnsFailure_WhenAlreadyClosed()
    {
        var creatorId = Guid.NewGuid();
        var poll = new Poll { Code = "abc12", Status = PollStatus.Closed, CreatorId = creatorId };
        _repo.Setup(r => r.GetByCodeAsync("abc12")).ReturnsAsync(poll);

        var result = await _sut.CloseAsync("abc12", creatorId);

        Assert.False(result.IsSuccess);
        Assert.Contains("already closed", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    // ── Delete (creator-only; logic ready for Phase 6) ──────────

    [Fact]
    public async Task Delete_ReturnsSuccess_WhenCreator()
    {
        var creatorId = Guid.NewGuid();
        var poll = new Poll { Code = "del01", CreatorId = creatorId };
        _repo.Setup(r => r.GetByCodeAsync("del01")).ReturnsAsync(poll);

        var result = await _sut.DeleteAsync("del01", creatorId);

        Assert.True(result.IsSuccess);
        _repo.Verify(r => r.DeleteAsync(poll), Times.Once);
    }

    [Fact]
    public async Task Delete_ReturnsFailure_WhenNotCreator()
    {
        var poll = new Poll { Code = "del01", CreatorId = Guid.NewGuid() };
        _repo.Setup(r => r.GetByCodeAsync("del01")).ReturnsAsync(poll);

        var result = await _sut.DeleteAsync("del01", Guid.NewGuid());

        Assert.False(result.IsSuccess);
        _repo.Verify(r => r.DeleteAsync(It.IsAny<Poll>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ReturnsFailure_WhenPollNotFound()
    {
        _repo.Setup(r => r.GetByCodeAsync("nope1")).ReturnsAsync((Poll?)null);

        var result = await _sut.DeleteAsync("nope1", Guid.NewGuid());

        Assert.False(result.IsSuccess);
    }

    // ── Admin bypass (RBAC) ─────────────────────────────────────

    [Fact]
    public async Task Close_ReturnsSuccess_WhenAdmin_EvenIfNotCreator()
    {
        var poll = new Poll { Code = "adm01", Status = PollStatus.Open, CreatorId = Guid.NewGuid() };
        _repo.Setup(r => r.GetByCodeAsync("adm01")).ReturnsAsync(poll);

        var result = await _sut.CloseAsync("adm01", Guid.NewGuid(), isAdmin: true);

        Assert.True(result.IsSuccess);
        _repo.Verify(r => r.UpdateAsync(poll), Times.Once);
    }

    [Fact]
    public async Task Delete_ReturnsSuccess_WhenAdmin_EvenIfNotCreator()
    {
        var poll = new Poll { Code = "adm02", CreatorId = Guid.NewGuid() };
        _repo.Setup(r => r.GetByCodeAsync("adm02")).ReturnsAsync(poll);

        var result = await _sut.DeleteAsync("adm02", Guid.NewGuid(), isAdmin: true);

        Assert.True(result.IsSuccess);
        _repo.Verify(r => r.DeleteAsync(poll), Times.Once);
    }

    [Fact]
    public async Task GetAll_ReturnsEveryPoll()
    {
        _repo.Setup(r => r.GetAllAsync(It.IsAny<int>())).ReturnsAsync(new List<Poll>
        {
            new() { Code = "all01", Question = "Q1" },
            new() { Code = "all02", Question = "Q2" }
        });

        var result = await _sut.GetAllAsync();

        Assert.Equal(2, result.Count());
    }

    // ── Expiry auto-close (Merit) ───────────────────────────────

    [Fact]
    public async Task CloseExpiredPolls_ClosesAllExpired_AndReturnsCount()
    {
        var expired = new List<Poll>
        {
            new() { Code = "exp01", Status = PollStatus.Open },
            new() { Code = "exp02", Status = PollStatus.Open }
        };
        _repo.Setup(r => r.GetExpiredAsync()).ReturnsAsync(expired);

        var count = await _sut.CloseExpiredPollsAsync();

        Assert.Equal(2, count);
        Assert.All(expired, p => Assert.Equal(PollStatus.Closed, p.Status));
        _repo.Verify(r => r.UpdateAsync(It.IsAny<Poll>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CloseExpiredPolls_ReturnsZero_WhenNoneExpired()
    {
        _repo.Setup(r => r.GetExpiredAsync()).ReturnsAsync(new List<Poll>());

        var count = await _sut.CloseExpiredPollsAsync();

        Assert.Equal(0, count);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<Poll>()), Times.Never);
    }
}
