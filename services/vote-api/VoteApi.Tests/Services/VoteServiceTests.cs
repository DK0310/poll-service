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
    private static readonly Guid Q1 = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Q2 = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

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

        // Safe defaults so BuildResultsAsync never dereferences a null list.
        _repo.Setup(r => r.HasVotedAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        _repo.Setup(r => r.GetVoteCountsAsync(It.IsAny<string>())).ReturnsAsync(new List<VoteCount>());
        _repo.Setup(r => r.GetTextAnswersAsync(It.IsAny<string>())).ReturnsAsync(new List<QuestionTextAnswer>());
        _repo.Setup(r => r.GetVoterCountAsync(It.IsAny<string>())).ReturnsAsync(0);
        _repo.Setup(r => r.GetSubmissionTimestampsAsync(It.IsAny<string>())).ReturnsAsync(new List<DateTime>());

        _sut = new VoteService(_repo.Object, _pollClient.Object, hub.Object);
    }

    // A poll with one SingleChoice question (id Q1) and `optionCount` options.
    private static PollInfo ActivePoll(string code = "abc12", int optionCount = 2) => new()
    {
        Code = code,
        Title = "Favorite color?",
        IsActive = true,
        Questions = new()
        {
            new PollQuestionInfo
            {
                Id = Q1,
                QuestionIndex = 0,
                Text = "Favorite color?",
                Type = "SingleChoice",
                Options = Enumerable.Range(0, optionCount)
                    .Select(i => new PollOptionInfo { OptionIndex = i, Text = $"Option {i}" })
                    .ToList()
            }
        }
    };

    private static PollInfo OpenTextPoll(string code = "ot01") => new()
    {
        Code = code,
        Title = "Thoughts?",
        IsActive = true,
        Questions = new()
        {
            new PollQuestionInfo { Id = Q1, QuestionIndex = 0, Text = "Thoughts?", Type = "OpenText" }
        }
    };

    // Builds a single-answer batch addressed to a question.
    private static VoteRequest Batch(Guid qid, int optionIndex, string voterToken,
        string? text = null, string? name = null, string? role = null) => new()
    {
        VoterToken = voterToken,
        AuthorName = name,
        AuthorRole = role,
        Answers = new() { new QuestionAnswer { QuestionId = qid, OptionIndex = optionIndex, TextAnswer = text } }
    };

    // ── Submit vote: success ────────────────────────────────────

    [Fact]
    public async Task SubmitVote_ReturnsSuccess_WhenValid()
    {
        _pollClient.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(ActivePoll());
        _repo.Setup(r => r.GetVoteCountsAsync("abc12"))
            .ReturnsAsync(new List<VoteCount> { new() { QuestionId = Q1, OptionIndex = 0, Count = 1 } });
        _repo.Setup(r => r.GetVoterCountAsync("abc12")).ReturnsAsync(1);

        var result = await _sut.SubmitVoteAsync("abc12", Batch(Q1, 0, "token123"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.TotalVoters);
        Assert.Single(result.Value.Questions);
        Assert.Equal(1, result.Value.Questions[0].TotalVotes);
        Assert.Equal(100, result.Value.Questions[0].Options[0].Percentage);
        _repo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Vote>>()), Times.Once);
        // Broadcasts updated results to the poll's SignalR group exactly once.
        _clientProxy.Verify(
            p => p.SendCoreAsync("ReceiveVoteUpdate", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SubmitVote_PersistsCorrectVote_WhenValid()
    {
        _pollClient.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(ActivePoll());
        List<Vote>? saved = null;
        _repo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Vote>>()))
            .Callback<IEnumerable<Vote>>(v => saved = v.ToList()).Returns(Task.CompletedTask);

        await _sut.SubmitVoteAsync("abc12", Batch(Q1, 1, "tok"));

        Assert.NotNull(saved);
        var vote = Assert.Single(saved!);
        Assert.Equal("abc12", vote.PollCode);
        Assert.Equal(Q1, vote.QuestionId);
        Assert.Equal(1, vote.OptionIndex);
        Assert.Equal("tok", vote.VoterToken);
    }

    [Fact]
    public async Task SubmitVote_PersistsOneRowPerQuestion_ForMultiQuestionPoll()
    {
        var poll = new PollInfo
        {
            Code = "multi1",
            Title = "Survey",
            IsActive = true,
            Questions = new()
            {
                new PollQuestionInfo { Id = Q1, QuestionIndex = 0, Text = "A?", Type = "YesNo",
                    Options = new() { new() { OptionIndex = 0, Text = "Yes" }, new() { OptionIndex = 1, Text = "No" } } },
                new PollQuestionInfo { Id = Q2, QuestionIndex = 1, Text = "B?", Type = "OpenText" }
            }
        };
        _pollClient.Setup(c => c.GetPollAsync("multi1")).ReturnsAsync(poll);
        List<Vote>? saved = null;
        _repo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Vote>>()))
            .Callback<IEnumerable<Vote>>(v => saved = v.ToList()).Returns(Task.CompletedTask);

        var request = new VoteRequest
        {
            VoterToken = "tok",
            Answers = new()
            {
                new QuestionAnswer { QuestionId = Q1, OptionIndex = 0 },
                new QuestionAnswer { QuestionId = Q2, TextAnswer = "Loved it" }
            }
        };

        var result = await _sut.SubmitVoteAsync("multi1", request);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, saved!.Count);
        Assert.Equal("Loved it", saved.Single(v => v.QuestionId == Q2).TextAnswer);
        _repo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Vote>>()), Times.Once);
    }

    // ── Submit vote: failures ───────────────────────────────────

    [Fact]
    public async Task SubmitVote_ReturnsFailure_WhenPollNotFound()
    {
        _pollClient.Setup(c => c.GetPollAsync("nope1")).ReturnsAsync((PollInfo?)null);

        var result = await _sut.SubmitVoteAsync("nope1", Batch(Q1, 0, "t"));

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error!, StringComparison.OrdinalIgnoreCase);
        _repo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Vote>>()), Times.Never);
    }

    [Fact]
    public async Task SubmitVote_ReturnsFailure_WhenPollClosed()
    {
        var poll = ActivePoll("cls01") with { IsActive = false };
        _pollClient.Setup(c => c.GetPollAsync("cls01")).ReturnsAsync(poll);

        var result = await _sut.SubmitVoteAsync("cls01", Batch(Q1, 0, "t"));

        Assert.False(result.IsSuccess);
        Assert.Contains("closed", result.Error!, StringComparison.OrdinalIgnoreCase);
        _repo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Vote>>()), Times.Never);
    }

    [Fact]
    public async Task SubmitVote_ReturnsFailure_WhenOptionIndexOutOfRange()
    {
        _pollClient.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(ActivePoll(optionCount: 2));

        var result = await _sut.SubmitVoteAsync("abc12", Batch(Q1, 5, "t"));

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid option", result.Error!);
    }

    [Fact]
    public async Task SubmitVote_ReturnsFailure_WhenOptionIndexNegative()
    {
        _pollClient.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(ActivePoll());

        var result = await _sut.SubmitVoteAsync("abc12", Batch(Q1, -1, "t"));

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid option", result.Error!);
    }

    [Fact]
    public async Task SubmitVote_ReturnsFailure_WhenVoterTokenEmpty()
    {
        _pollClient.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(ActivePoll());

        var result = await _sut.SubmitVoteAsync("abc12", Batch(Q1, 0, ""));

        Assert.False(result.IsSuccess);
        _repo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Vote>>()), Times.Never);
    }

    [Fact]
    public async Task SubmitVote_ReturnsFailure_WhenDuplicateVote()
    {
        _pollClient.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(ActivePoll());
        _repo.Setup(r => r.HasVotedAsync("abc12", "dup")).ReturnsAsync(true);

        var result = await _sut.SubmitVoteAsync("abc12", Batch(Q1, 0, "dup"));

        Assert.False(result.IsSuccess);
        Assert.Contains("already voted", result.Error!, StringComparison.OrdinalIgnoreCase);
        _repo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Vote>>()), Times.Never);
        // No broadcast when the vote is rejected.
        _clientProxy.Verify(
            p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SubmitVote_ReturnsFailure_WhenAnswerReferencesUnknownQuestion()
    {
        _pollClient.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(ActivePoll());

        var result = await _sut.SubmitVoteAsync("abc12", Batch(Guid.NewGuid(), 0, "t"));

        Assert.False(result.IsSuccess);
        Assert.Contains("unknown question", result.Error!, StringComparison.OrdinalIgnoreCase);
        _repo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Vote>>()), Times.Never);
    }

    [Fact]
    public async Task SubmitVote_ReturnsFailure_WhenNotEveryQuestionAnswered()
    {
        var poll = new PollInfo
        {
            Code = "multi2",
            IsActive = true,
            Questions = new()
            {
                new PollQuestionInfo { Id = Q1, QuestionIndex = 0, Text = "A?", Type = "YesNo",
                    Options = new() { new() { OptionIndex = 0, Text = "Yes" }, new() { OptionIndex = 1, Text = "No" } } },
                new PollQuestionInfo { Id = Q2, QuestionIndex = 1, Text = "B?", Type = "YesNo",
                    Options = new() { new() { OptionIndex = 0, Text = "Yes" }, new() { OptionIndex = 1, Text = "No" } } }
            }
        };
        _pollClient.Setup(c => c.GetPollAsync("multi2")).ReturnsAsync(poll);

        // Only answers Q1, not Q2.
        var result = await _sut.SubmitVoteAsync("multi2", Batch(Q1, 0, "t"));

        Assert.False(result.IsSuccess);
        Assert.Contains("every question", result.Error!, StringComparison.OrdinalIgnoreCase);
        _repo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Vote>>()), Times.Never);
    }

    // ── Open-text question type (Merit) ─────────────────────────

    [Fact]
    public async Task SubmitVote_OpenText_StoresTextAnswer_AndReturnsAnswers()
    {
        _pollClient.Setup(c => c.GetPollAsync("ot01")).ReturnsAsync(OpenTextPoll());
        _repo.Setup(r => r.GetTextAnswersAsync("ot01"))
            .ReturnsAsync(new List<QuestionTextAnswer>
            {
                new() { QuestionId = Q1, Answer = new TextAnswerResponse { Text = "Great poll" } }
            });
        List<Vote>? saved = null;
        _repo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Vote>>()))
            .Callback<IEnumerable<Vote>>(v => saved = v.ToList()).Returns(Task.CompletedTask);

        var result = await _sut.SubmitVoteAsync("ot01", Batch(Q1, 0, "t", text: "Great poll"));

        Assert.True(result.IsSuccess);
        Assert.Equal("OpenText", result.Value!.Questions[0].Type);
        Assert.Contains(result.Value.Questions[0].TextAnswers, a => a.Text == "Great poll");
        Assert.Equal("Great poll", saved!.Single().TextAnswer);
    }

    [Fact]
    public async Task SubmitVote_OpenText_PersistsAuthorLabel_WhenLoggedIn()
    {
        _pollClient.Setup(c => c.GetPollAsync("ot01")).ReturnsAsync(OpenTextPoll());
        List<Vote>? saved = null;
        _repo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Vote>>()))
            .Callback<IEnumerable<Vote>>(v => saved = v.ToList()).Returns(Task.CompletedTask);

        var result = await _sut.SubmitVoteAsync("ot01",
            Batch(Q1, 0, "t", text: "Nice", name: "alice", role: "User"));

        Assert.True(result.IsSuccess);
        Assert.Equal("alice", saved!.Single().AuthorName);
        Assert.Equal("User", saved!.Single().AuthorRole);
    }

    [Fact]
    public async Task SubmitVote_OpenText_AuthorIsNull_ForGuest()
    {
        _pollClient.Setup(c => c.GetPollAsync("ot01")).ReturnsAsync(OpenTextPoll());
        List<Vote>? saved = null;
        _repo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Vote>>()))
            .Callback<IEnumerable<Vote>>(v => saved = v.ToList()).Returns(Task.CompletedTask);

        var result = await _sut.SubmitVoteAsync("ot01", Batch(Q1, 0, "t", text: "Hi"));

        Assert.True(result.IsSuccess);
        Assert.Null(saved!.Single().AuthorName);
        Assert.Null(saved!.Single().AuthorRole);
    }

    [Fact]
    public async Task SubmitVote_OpenText_ReturnsFailure_WhenTextEmpty()
    {
        _pollClient.Setup(c => c.GetPollAsync("ot01")).ReturnsAsync(OpenTextPoll());

        var result = await _sut.SubmitVoteAsync("ot01", Batch(Q1, 0, "t", text: ""));

        Assert.False(result.IsSuccess);
        _repo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Vote>>()), Times.Never);
    }

    // ── Get results ─────────────────────────────────────────────

    [Fact]
    public async Task GetResults_ReturnsAggregatedCounts_WithPercentages()
    {
        _pollClient.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(ActivePoll(optionCount: 2));
        _repo.Setup(r => r.GetVoteCountsAsync("abc12")).ReturnsAsync(new List<VoteCount>
        {
            new() { QuestionId = Q1, OptionIndex = 0, Count = 3 },
            new() { QuestionId = Q1, OptionIndex = 1, Count = 1 }
        });

        var result = await _sut.GetResultsAsync("abc12");

        Assert.True(result.IsSuccess);
        var question = result.Value!.Questions[0];
        Assert.Equal(4, question.TotalVotes);
        Assert.Equal(75, question.Options[0].Percentage);
        Assert.Equal(25, question.Options[1].Percentage);
    }

    [Fact]
    public async Task GetResults_ReturnsZeroPercentages_WhenNoVotes()
    {
        _pollClient.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(ActivePoll());
        _repo.Setup(r => r.GetVoteCountsAsync("abc12")).ReturnsAsync(new List<VoteCount>());

        var result = await _sut.GetResultsAsync("abc12");

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.Questions[0].TotalVotes);
        Assert.All(result.Value.Questions[0].Options, o => Assert.Equal(0, o.Percentage));
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
    public async Task GetAnalytics_ReturnsPerQuestionTopOption_AndPeakMinute()
    {
        _pollClient.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(ActivePoll(optionCount: 2));
        _repo.Setup(r => r.GetVoteCountsAsync("abc12")).ReturnsAsync(new List<VoteCount>
        {
            new() { QuestionId = Q1, OptionIndex = 0, Count = 3 },
            new() { QuestionId = Q1, OptionIndex = 1, Count = 1 }
        });
        var minute1 = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var minute2 = new DateTime(2026, 1, 1, 10, 1, 0, DateTimeKind.Utc);
        // Four distinct voters: three in minute1, one in minute2.
        _repo.Setup(r => r.GetSubmissionTimestampsAsync("abc12")).ReturnsAsync(new List<DateTime>
        {
            minute1, minute1.AddSeconds(20), minute1.AddSeconds(40), minute2.AddSeconds(5)
        });

        var result = await _sut.GetAnalyticsAsync("abc12", userId: null, isAdmin: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value!.TotalVoters);
        var q = Assert.Single(result.Value.Questions);
        Assert.Equal(4, q.TotalVotes);
        Assert.Equal(0, q.TopOption!.OptionIndex);
        Assert.Equal(3, q.TopOption.VoteCount);
        Assert.Equal(2, result.Value.Timeline.Count);          // two distinct minutes
        Assert.Equal(minute1, result.Value.PeakMinute!.Minute); // busiest minute
        Assert.Equal(3, result.Value.PeakMinute.Count);
    }

    [Fact]
    public async Task GetAnalytics_IsEmpty_WhenNoVotes()
    {
        _pollClient.Setup(c => c.GetPollAsync("abc12")).ReturnsAsync(ActivePoll());
        _repo.Setup(r => r.GetVoteCountsAsync("abc12")).ReturnsAsync(new List<VoteCount>());
        _repo.Setup(r => r.GetSubmissionTimestampsAsync("abc12")).ReturnsAsync(new List<DateTime>());

        var result = await _sut.GetAnalyticsAsync("abc12", userId: null, isAdmin: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.TotalVoters);
        Assert.Null(result.Value.Questions[0].TopOption);
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
        _repo.Setup(r => r.GetSubmissionTimestampsAsync("own01")).ReturnsAsync(new List<DateTime>());

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
