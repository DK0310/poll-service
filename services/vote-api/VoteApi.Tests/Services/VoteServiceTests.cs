using Microsoft.AspNetCore.SignalR;
using Moq;
using VoteApi.DTOs;
using VoteApi.Hubs;
using VoteApi.Models;
using VoteApi.Repositories;
using VoteApi.Services;

namespace VoteApi.Tests.Services;

public class VoteServiceTests
{
    private readonly Mock<VoteRepository> _repo;
    private readonly Mock<PollClientService> _pollClient;
    private readonly Mock<IClientProxy> _clientProxy;
    private readonly VoteService _sut;

    public VoteServiceTests()
    {
        // Collaborators have constructor dependencies (DbContext / HttpClient);
        // pass null since every used method is mocked (virtual).
        _repo = new Mock<VoteRepository>(MockBehavior.Loose, new object[] { null! });
        _pollClient = new Mock<PollClientService>(MockBehavior.Loose, new object[] { null! });

        // SignalR hub mock: Clients.Group(code) → IClientProxy (whose SendCoreAsync we verify).
        _clientProxy = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxy.Object);
        var hub = new Mock<IHubContext<PollHub>>();
        hub.Setup(h => h.Clients).Returns(clients.Object);

        _sut = new VoteService(_repo.Object, _pollClient.Object, hub.Object);
    }

    private static PollInfo ActivePoll(string code = "abc12", int optionCount = 2) => new()
    {
        Code = code,
        Question = "Favorite color?",
        IsActive = true,
        Options = Enumerable.Range(0, optionCount)
            .Select(i => new PollOptionInfo { OptionIndex = i, Text = $"Option {i}" })
            .ToList()
    };

    // ── Submit vote: success ────────────────────────────────────

    [Fact]
    public async Task SubmitVote_ReturnsSuccess_WhenValid()
    {
        _pollClient.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(ActivePoll());
        _repo.Setup(r => r.HasVotedAsync("abc12", "token123")).ReturnsAsync(false);
        _repo.Setup(r => r.GetVoteCountsAsync("abc12"))
            .ReturnsAsync(new List<VoteCount> { new() { OptionIndex = 0, Count = 1 } });

        var result = await _sut.SubmitVoteAsync("abc12", new VoteRequest { OptionIndex = 0, VoterToken = "token123" });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.TotalVotes);
        Assert.Equal(100, result.Value.Options[0].Percentage);
        _repo.Verify(r => r.AddAsync(It.IsAny<Vote>()), Times.Once);
        // Broadcasts updated results to the poll's SignalR group exactly once.
        _clientProxy.Verify(
            p => p.SendCoreAsync("ReceiveVoteUpdate", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SubmitVote_PersistsCorrectVote_WhenValid()
    {
        _pollClient.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(ActivePoll());
        _repo.Setup(r => r.HasVotedAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        _repo.Setup(r => r.GetVoteCountsAsync(It.IsAny<string>())).ReturnsAsync(new List<VoteCount>());
        Vote? saved = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<Vote>())).Callback<Vote>(v => saved = v).Returns(Task.CompletedTask);

        await _sut.SubmitVoteAsync("abc12", new VoteRequest { OptionIndex = 1, VoterToken = "tok" });

        Assert.NotNull(saved);
        Assert.Equal("abc12", saved!.PollCode);
        Assert.Equal(1, saved.OptionIndex);
        Assert.Equal("tok", saved.VoterToken);
    }

    // ── Submit vote: failures ───────────────────────────────────

    [Fact]
    public async Task SubmitVote_ReturnsFailure_WhenPollNotFound()
    {
        _pollClient.Setup(c => c.GetPollAsync("nope1")).ReturnsAsync((PollInfo?)null);

        var result = await _sut.SubmitVoteAsync("nope1", new VoteRequest { OptionIndex = 0, VoterToken = "t" });

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error!, StringComparison.OrdinalIgnoreCase);
        _repo.Verify(r => r.AddAsync(It.IsAny<Vote>()), Times.Never);
    }

    [Fact]
    public async Task SubmitVote_ReturnsFailure_WhenPollClosed()
    {
        var poll = ActivePoll("cls01") with { IsActive = false };
        _pollClient.Setup(c => c.GetPollAsync("cls01")).ReturnsAsync(poll);

        var result = await _sut.SubmitVoteAsync("cls01", new VoteRequest { OptionIndex = 0, VoterToken = "t" });

        Assert.False(result.IsSuccess);
        Assert.Contains("closed", result.Error!, StringComparison.OrdinalIgnoreCase);
        _repo.Verify(r => r.AddAsync(It.IsAny<Vote>()), Times.Never);
    }

    [Fact]
    public async Task SubmitVote_ReturnsFailure_WhenOptionIndexOutOfRange()
    {
        _pollClient.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(ActivePoll(optionCount: 2));

        var result = await _sut.SubmitVoteAsync("abc12", new VoteRequest { OptionIndex = 5, VoterToken = "t" });

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid option", result.Error!);
    }

    [Fact]
    public async Task SubmitVote_ReturnsFailure_WhenOptionIndexNegative()
    {
        _pollClient.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(ActivePoll());

        var result = await _sut.SubmitVoteAsync("abc12", new VoteRequest { OptionIndex = -1, VoterToken = "t" });

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid option", result.Error!);
    }

    [Fact]
    public async Task SubmitVote_ReturnsFailure_WhenVoterTokenEmpty()
    {
        _pollClient.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(ActivePoll());

        var result = await _sut.SubmitVoteAsync("abc12", new VoteRequest { OptionIndex = 0, VoterToken = "" });

        Assert.False(result.IsSuccess);
        _repo.Verify(r => r.AddAsync(It.IsAny<Vote>()), Times.Never);
    }

    [Fact]
    public async Task SubmitVote_ReturnsFailure_WhenDuplicateVote()
    {
        _pollClient.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(ActivePoll());
        _repo.Setup(r => r.HasVotedAsync("abc12", "dup")).ReturnsAsync(true);

        var result = await _sut.SubmitVoteAsync("abc12", new VoteRequest { OptionIndex = 0, VoterToken = "dup" });

        Assert.False(result.IsSuccess);
        Assert.Contains("already voted", result.Error!, StringComparison.OrdinalIgnoreCase);
        _repo.Verify(r => r.AddAsync(It.IsAny<Vote>()), Times.Never);
        // No broadcast when the vote is rejected.
        _clientProxy.Verify(
            p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Open-text question type (Merit) ─────────────────────────

    [Fact]
    public async Task SubmitVote_OpenText_StoresTextAnswer_AndReturnsAnswers()
    {
        var poll = ActivePoll("ot01") with { Type = "OpenText", Options = new() };
        _pollClient.Setup(c => c.GetPollAsync("ot01")).ReturnsAsync(poll);
        _repo.Setup(r => r.HasVotedAsync("ot01", "t")).ReturnsAsync(false);
        _repo.Setup(r => r.GetTextAnswersAsync("ot01"))
            .ReturnsAsync(new List<TextAnswerResponse> { new() { Text = "Great poll" } });
        Vote? saved = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<Vote>())).Callback<Vote>(v => saved = v).Returns(Task.CompletedTask);

        var result = await _sut.SubmitVoteAsync("ot01", new VoteRequest { TextAnswer = "Great poll", VoterToken = "t" });

        Assert.True(result.IsSuccess);
        Assert.Equal("OpenText", result.Value!.Type);
        Assert.Contains(result.Value.TextAnswers, a => a.Text == "Great poll");
        Assert.Equal("Great poll", saved!.TextAnswer);
    }

    [Fact]
    public async Task SubmitVote_OpenText_PersistsAuthorLabel_WhenLoggedIn()
    {
        var poll = ActivePoll("ot01") with { Type = "OpenText", Options = new() };
        _pollClient.Setup(c => c.GetPollAsync("ot01")).ReturnsAsync(poll);
        _repo.Setup(r => r.HasVotedAsync("ot01", "t")).ReturnsAsync(false);
        _repo.Setup(r => r.GetTextAnswersAsync("ot01")).ReturnsAsync(new List<TextAnswerResponse>());
        Vote? saved = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<Vote>())).Callback<Vote>(v => saved = v).Returns(Task.CompletedTask);

        var result = await _sut.SubmitVoteAsync("ot01",
            new VoteRequest { TextAnswer = "Nice", VoterToken = "t", AuthorName = "alice", AuthorRole = "User" });

        Assert.True(result.IsSuccess);
        Assert.Equal("alice", saved!.AuthorName);
        Assert.Equal("User", saved.AuthorRole);
    }

    [Fact]
    public async Task SubmitVote_OpenText_AuthorIsNull_ForGuest()
    {
        var poll = ActivePoll("ot01") with { Type = "OpenText", Options = new() };
        _pollClient.Setup(c => c.GetPollAsync("ot01")).ReturnsAsync(poll);
        _repo.Setup(r => r.HasVotedAsync("ot01", "t")).ReturnsAsync(false);
        _repo.Setup(r => r.GetTextAnswersAsync("ot01")).ReturnsAsync(new List<TextAnswerResponse>());
        Vote? saved = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<Vote>())).Callback<Vote>(v => saved = v).Returns(Task.CompletedTask);

        var result = await _sut.SubmitVoteAsync("ot01", new VoteRequest { TextAnswer = "Hi", VoterToken = "t" });

        Assert.True(result.IsSuccess);
        Assert.Null(saved!.AuthorName);
        Assert.Null(saved.AuthorRole);
    }

    [Fact]
    public async Task SubmitVote_OpenText_ReturnsFailure_WhenTextEmpty()
    {
        var poll = ActivePoll("ot01") with { Type = "OpenText", Options = new() };
        _pollClient.Setup(c => c.GetPollAsync("ot01")).ReturnsAsync(poll);

        var result = await _sut.SubmitVoteAsync("ot01", new VoteRequest { TextAnswer = "", VoterToken = "t" });

        Assert.False(result.IsSuccess);
        _repo.Verify(r => r.AddAsync(It.IsAny<Vote>()), Times.Never);
    }

    // ── Get results ─────────────────────────────────────────────

    [Fact]
    public async Task GetResults_ReturnsAggregatedCounts_WithPercentages()
    {
        _pollClient.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(ActivePoll(optionCount: 2));
        _repo.Setup(r => r.GetVoteCountsAsync("abc12")).ReturnsAsync(new List<VoteCount>
        {
            new() { OptionIndex = 0, Count = 3 },
            new() { OptionIndex = 1, Count = 1 }
        });

        var result = await _sut.GetResultsAsync("abc12");

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value!.TotalVotes);
        Assert.Equal(75, result.Value.Options[0].Percentage);
        Assert.Equal(25, result.Value.Options[1].Percentage);
    }

    [Fact]
    public async Task GetResults_ReturnsZeroPercentages_WhenNoVotes()
    {
        _pollClient.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(ActivePoll());
        _repo.Setup(r => r.GetVoteCountsAsync("abc12")).ReturnsAsync(new List<VoteCount>());

        var result = await _sut.GetResultsAsync("abc12");

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.TotalVotes);
        Assert.All(result.Value.Options, o => Assert.Equal(0, o.Percentage));
    }

    [Fact]
    public async Task GetResults_ReturnsFailure_WhenPollNotFound()
    {
        _pollClient.Setup(c => c.GetPollAsync("nope1")).ReturnsAsync((PollInfo?)null);

        var result = await _sut.GetResultsAsync("nope1");

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    // ── Analytics (Distinction) ─────────────────────────────────

    [Fact]
    public async Task GetAnalytics_ReturnsTopOptionAndPeakMinute()
    {
        _pollClient.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(ActivePoll(optionCount: 2));
        _repo.Setup(r => r.GetVoteCountsAsync("abc12")).ReturnsAsync(new List<VoteCount>
        {
            new() { OptionIndex = 0, Count = 3 },
            new() { OptionIndex = 1, Count = 1 }
        });
        var minute1 = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var minute2 = new DateTime(2026, 1, 1, 10, 1, 0, DateTimeKind.Utc);
        _repo.Setup(r => r.GetVoteTimestampsAsync("abc12")).ReturnsAsync(new List<DateTime>
        {
            minute1, minute1.AddSeconds(20), minute1.AddSeconds(40), minute2.AddSeconds(5)
        });

        var result = await _sut.GetAnalyticsAsync("abc12", userId: null, isAdmin: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value!.TotalVotes);
        Assert.Equal(0, result.Value.TopOption!.OptionIndex);
        Assert.Equal(3, result.Value.TopOption.VoteCount);
        Assert.Equal(2, result.Value.Timeline.Count);          // two distinct minutes
        Assert.Equal(minute1, result.Value.PeakMinute!.Minute); // busiest minute
        Assert.Equal(3, result.Value.PeakMinute.Count);
    }

    [Fact]
    public async Task GetAnalytics_IsEmpty_WhenNoVotes()
    {
        _pollClient.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(ActivePoll());
        _repo.Setup(r => r.GetVoteCountsAsync("abc12")).ReturnsAsync(new List<VoteCount>());
        _repo.Setup(r => r.GetVoteTimestampsAsync("abc12")).ReturnsAsync(new List<DateTime>());

        var result = await _sut.GetAnalyticsAsync("abc12", userId: null, isAdmin: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.TotalVotes);
        Assert.Null(result.Value.TopOption);
        Assert.Null(result.Value.PeakMinute);
        Assert.Empty(result.Value.Timeline);
    }

    [Fact]
    public async Task GetAnalytics_ReturnsFailure_WhenPollNotFound()
    {
        _pollClient.Setup(c => c.GetPollAsync("nope1")).ReturnsAsync((PollInfo?)null);

        var result = await _sut.GetAnalyticsAsync("nope1", userId: null, isAdmin: true);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetAnalytics_Succeeds_WhenOwner()
    {
        var owner = Guid.NewGuid();
        _pollClient.Setup(c => c.GetPollAsync("own01")).ReturnsAsync(ActivePoll("own01") with { CreatorId = owner });
        _repo.Setup(r => r.GetVoteCountsAsync("own01")).ReturnsAsync(new List<VoteCount>());
        _repo.Setup(r => r.GetVoteTimestampsAsync("own01")).ReturnsAsync(new List<DateTime>());

        var result = await _sut.GetAnalyticsAsync("own01", userId: owner, isAdmin: false);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetAnalytics_ReturnsForbidden_WhenNotOwnerNorAdmin()
    {
        _pollClient.Setup(c => c.GetPollAsync("own02")).ReturnsAsync(ActivePoll("own02") with { CreatorId = Guid.NewGuid() });

        var result = await _sut.GetAnalyticsAsync("own02", userId: Guid.NewGuid(), isAdmin: false);

        Assert.False(result.IsSuccess);
        Assert.Contains("forbidden", result.Error!, StringComparison.OrdinalIgnoreCase);
    }
}
