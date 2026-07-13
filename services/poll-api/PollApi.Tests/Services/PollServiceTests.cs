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

    // A single-question survey (the common case in these tests).
    private static CreatePollRequest Request(string question, params string[] options) =>
        new() { Questions = new() { new CreateQuestionRequest { Text = question, Options = options.ToList() } } };

    private static CreateQuestionRequest Q(string text, string type = "SingleChoice", params string[] options) =>
        new() { Text = text, Type = type, Options = options.ToList() };

    // ── Create: success ─────────────────────────────────────────

    [Fact]
    public async Task Create_ReturnsSuccess_WhenValid()
    {
        _repo.Setup(r => r.GetByCodeAsync(It.IsAny<string>())).ReturnsAsync((Poll?)null);

        var result = await _sut.CreateAsync(Request("Favorite color?", "Red", "Blue", "Green"), null);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Questions);
        Assert.Equal("Favorite color?", result.Value.Questions[0].Text);
        Assert.Equal(3, result.Value.Questions[0].Options.Count);
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
            Questions = new() { Q("Lunch?", "SingleChoice", "Pizza", "Sushi") },
            ExpiryHours = 24
        };

        var result = await _sut.CreateAsync(request, null);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.ExpiresAt);
    }

    [Fact]
    public async Task Create_PersistsTitle_AndMultipleQuestions_WithOrder()
    {
        _repo.Setup(r => r.GetByCodeAsync(It.IsAny<string>())).ReturnsAsync((Poll?)null);
        Poll? saved = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<Poll>())).Callback<Poll>(p => saved = p).Returns(Task.CompletedTask);

        var request = new CreatePollRequest
        {
            Title = "Event feedback",
            Questions = new()
            {
                Q("Overall?", "Rating"),
                Q("Recommend?", "YesNo"),
                Q("Favorite talk?", "SingleChoice", "Keynote", "Workshop")
            }
        };

        var result = await _sut.CreateAsync(request, null);

        Assert.True(result.IsSuccess);
        Assert.Equal("Event feedback", result.Value!.Title);
        Assert.Equal(3, result.Value.Questions.Count);
        // Questions keep their submitted order via QuestionIndex.
        Assert.Equal(new[] { 0, 1, 2 }, result.Value.Questions.Select(q => q.QuestionIndex).ToArray());
        Assert.Equal(new[] { "Rating", "YesNo", "SingleChoice" }, result.Value.Questions.Select(q => q.Type).ToArray());
        Assert.Equal(5, saved!.Questions.First(q => q.Type == PollQuestionType.Rating).Options.Count);
    }

    // ── Create: validation failures ─────────────────────────────

    [Fact]
    public async Task Create_ReturnsFailure_WhenNoQuestions()
    {
        var result = await _sut.CreateAsync(new CreatePollRequest { Questions = new() }, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("at least one question", result.Error!, StringComparison.OrdinalIgnoreCase);
        _repo.Verify(r => r.AddAsync(It.IsAny<Poll>()), Times.Never);
    }

    [Fact]
    public async Task Create_ReturnsFailure_WhenQuestionTextEmpty()
    {
        var result = await _sut.CreateAsync(Request("", "A", "B"), null);

        Assert.False(result.IsSuccess);
        Assert.Contains("text is required", result.Error!, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public async Task Create_LabelsError_WithOffendingQuestionNumber()
    {
        // First question is valid; the second is invalid (too few options).
        var request = new CreatePollRequest
        {
            Questions = new()
            {
                Q("Good one?", "YesNo"),
                Q("Bad one?", "SingleChoice", "only-one")
            }
        };

        var result = await _sut.CreateAsync(request, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("Question 2", result.Error!);
        _repo.Verify(r => r.AddAsync(It.IsAny<Poll>()), Times.Never);
    }

    // ── Create: question types (Merit) ──────────────────────────

    [Fact]
    public async Task Create_YesNo_GeneratesYesNoOptions()
    {
        _repo.Setup(r => r.GetByCodeAsync(It.IsAny<string>())).ReturnsAsync((Poll?)null);

        var result = await _sut.CreateAsync(new CreatePollRequest { Questions = new() { Q("Agree?", "YesNo") } }, null);

        Assert.True(result.IsSuccess);
        Assert.Equal("YesNo", result.Value!.Questions[0].Type);
        Assert.Equal(new[] { "Yes", "No" }, result.Value.Questions[0].Options.Select(o => o.Text).ToArray());
    }

    [Fact]
    public async Task Create_Rating_GeneratesFiveOptions()
    {
        _repo.Setup(r => r.GetByCodeAsync(It.IsAny<string>())).ReturnsAsync((Poll?)null);

        var result = await _sut.CreateAsync(new CreatePollRequest { Questions = new() { Q("Rate it", "Rating") } }, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value!.Questions[0].Options.Count);
        Assert.Equal("5", result.Value.Questions[0].Options[4].Text);
    }

    [Fact]
    public async Task Create_OpenText_HasNoOptions()
    {
        _repo.Setup(r => r.GetByCodeAsync(It.IsAny<string>())).ReturnsAsync((Poll?)null);

        var result = await _sut.CreateAsync(new CreatePollRequest { Questions = new() { Q("Thoughts?", "OpenText") } }, null);

        Assert.True(result.IsSuccess);
        Assert.Equal("OpenText", result.Value!.Questions[0].Type);
        Assert.Empty(result.Value.Questions[0].Options);
    }

    [Fact]
    public async Task Create_ReturnsFailure_WhenInvalidType()
    {
        var result = await _sut.CreateAsync(
            new CreatePollRequest { Questions = new() { Q("Q", "Bogus", "a", "b") } }, null);

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
            Title = "Test survey",
            Questions = new List<Question>
            {
                new()
                {
                    QuestionIndex = 0,
                    Text = "Test?",
                    Options = new List<PollOption>
                    {
                        new() { OptionIndex = 1, Text = "B" },
                        new() { OptionIndex = 0, Text = "A" }
                    }
                }
            }
        };
        _repo.Setup(r => r.GetByCodeAsync("abc12")).ReturnsAsync(poll);

        var result = await _sut.GetByCodeAsync("abc12");

        Assert.True(result.IsSuccess);
        Assert.Equal("Test?", result.Value!.Questions[0].Text);
        // Options come back ordered by index
        Assert.Equal(0, result.Value.Questions[0].Options[0].OptionIndex);
        Assert.Equal(1, result.Value.Questions[0].Options[1].OptionIndex);
    }

    [Fact]
    public async Task GetByCode_ReturnsFailure_WhenNotFound()
    {
        _repo.Setup(r => r.GetByCodeAsync("nope1")).ReturnsAsync((Poll?)null);

        var result = await _sut.GetByCodeAsync("nope1");

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetByCode_LazilyClosesExpiredOpenPoll()
    {
        // Past expiry but still Open (the background sweep hasn't run yet).
        var poll = new Poll
        {
            Code = "exp01",
            Status = PollStatus.Open,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        };
        _repo.Setup(r => r.GetByCodeAsync("exp01")).ReturnsAsync(poll);

        var result = await _sut.GetByCodeAsync("exp01");

        Assert.True(result.IsSuccess);
        Assert.Equal("Closed", result.Value!.Status);   // persisted close reflected in the response
        Assert.False(result.Value.IsActive);
        Assert.Equal(PollStatus.Closed, poll.Status);
        _repo.Verify(r => r.UpdateAsync(poll), Times.Once); // close was persisted
    }

    [Fact]
    public async Task GetByCode_DoesNotPersist_WhenNotExpired()
    {
        var poll = new Poll
        {
            Code = "act01",
            Status = PollStatus.Open,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        _repo.Setup(r => r.GetByCodeAsync("act01")).ReturnsAsync(poll);

        var result = await _sut.GetByCodeAsync("act01");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsActive);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<Poll>()), Times.Never);
    }

    // ── Close (creator-only) ────────────────────────────────────

    [Fact]
    public async Task Close_ReturnsSuccess_WhenCreator()
    {
        var creatorId = Guid.NewGuid();
        var poll = new Poll { Code = "abc12", Status = PollStatus.Open, CreatorId = creatorId };
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

    // ── Delete (creator-only) ───────────────────────────────────

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
            new() { Code = "all01", Title = "Q1" },
            new() { Code = "all02", Title = "Q2" }
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
