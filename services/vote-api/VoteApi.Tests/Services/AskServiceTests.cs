using Microsoft.AspNetCore.SignalR;
using Moq;
using VoteApi.DTOs;
using VoteApi.Hubs;
using VoteApi.Models;
using VoteApi.Repositories;
using VoteApi.Services;

namespace VoteApi.Tests.Services;

public class AskServiceTests
{
    private readonly Mock<AskRepository> _repo;
    private readonly Mock<PollClientService> _pollClient;
    private readonly Mock<IClientProxy> _clientProxy;
    private readonly AskService _sut;

    public AskServiceTests()
    {
        _repo = new Mock<AskRepository>(MockBehavior.Loose, new object[] { null! });
        _pollClient = new Mock<PollClientService>(MockBehavior.Loose, new object[] { null! });

        _clientProxy = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxy.Object);
        var hub = new Mock<IHubContext<PollHub>>();
        hub.Setup(h => h.Clients).Returns(clients.Object);

        // Defaults: poll exists; broadcast list query returns empty (avoids NRE in BroadcastAsync).
        _pollClient.Setup(c => c.GetPollAsync(It.IsAny<string>()))
            .ReturnsAsync(new PollInfo { Code = "p", IsActive = true });
        _repo.Setup(r => r.GetByPollAsync(It.IsAny<string>())).ReturnsAsync(new List<AudienceQuestion>());

        _sut = new AskService(_repo.Object, _pollClient.Object, hub.Object);
    }

    [Fact]
    public async Task Submit_AddsQuestion_AndBroadcasts()
    {
        AudienceQuestion? saved = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<AudienceQuestion>())).Callback<AudienceQuestion>(q => saved = q).Returns(Task.CompletedTask);

        var result = await _sut.SubmitAsync("p", new SubmitAskRequest { Text = "Why microservices?" });

        Assert.True(result.IsSuccess);
        Assert.Equal("Why microservices?", saved!.Text);
        _repo.Verify(r => r.AddAsync(It.IsAny<AudienceQuestion>()), Times.Once);
        _clientProxy.Verify(
            p => p.SendCoreAsync("ReceiveAskUpdate", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Submit_ReturnsFailure_WhenPollNotFound()
    {
        _pollClient.Setup(c => c.GetPollAsync("nope")).ReturnsAsync((PollInfo?)null);

        var result = await _sut.SubmitAsync("nope", new SubmitAskRequest { Text = "hi" });

        Assert.False(result.IsSuccess);
        _repo.Verify(r => r.AddAsync(It.IsAny<AudienceQuestion>()), Times.Never);
    }

    [Fact]
    public async Task Submit_ReturnsFailure_WhenTextEmpty()
    {
        var result = await _sut.SubmitAsync("p", new SubmitAskRequest { Text = "   " });

        Assert.False(result.IsSuccess);
        _repo.Verify(r => r.AddAsync(It.IsAny<AudienceQuestion>()), Times.Never);
    }

    [Fact]
    public async Task Upvote_IncrementsCount_AndRecordsVoter()
    {
        var q = new AudienceQuestion { PollCode = "p", Text = "Q", Upvotes = 2 };
        _repo.Setup(r => r.GetByIdAsync(q.Id)).ReturnsAsync(q);
        _repo.Setup(r => r.HasUpvotedAsync(q.Id, "voter-1")).ReturnsAsync(false);

        var result = await _sut.UpvoteAsync("p", q.Id, "voter-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Upvotes);
        _repo.Verify(r => r.AddUpvoteAsync(q.Id, "voter-1"), Times.Once);
        _repo.Verify(r => r.UpdateAsync(q), Times.Once);
    }

    [Fact]
    public async Task Upvote_ReturnsConflict_WhenAlreadyUpvoted()
    {
        var q = new AudienceQuestion { PollCode = "p", Text = "Q", Upvotes = 1 };
        _repo.Setup(r => r.GetByIdAsync(q.Id)).ReturnsAsync(q);
        _repo.Setup(r => r.HasUpvotedAsync(q.Id, "voter-1")).ReturnsAsync(true);

        var result = await _sut.UpvoteAsync("p", q.Id, "voter-1");

        Assert.False(result.IsSuccess);
        Assert.Contains("already upvoted", result.Error!, StringComparison.OrdinalIgnoreCase);
        _repo.Verify(r => r.AddUpvoteAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<AudienceQuestion>()), Times.Never);
    }

    [Fact]
    public async Task Upvote_ReturnsFailure_WhenNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((AudienceQuestion?)null);

        var result = await _sut.UpvoteAsync("p", Guid.NewGuid(), "voter-1");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task TogglePin_FlipsPinnedState_WhenAdmin()
    {
        var q = new AudienceQuestion { PollCode = "p", Text = "Q", IsPinned = false };
        _repo.Setup(r => r.GetByIdAsync(q.Id)).ReturnsAsync(q);

        var result = await _sut.TogglePinAsync("p", q.Id, userId: null, isAdmin: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsPinned);
    }

    [Fact]
    public async Task TogglePin_Succeeds_WhenOwner()
    {
        var owner = Guid.NewGuid();
        var q = new AudienceQuestion { PollCode = "p", Text = "Q", IsPinned = false };
        _repo.Setup(r => r.GetByIdAsync(q.Id)).ReturnsAsync(q);
        _pollClient.Setup(c => c.GetPollAsync("p"))
            .ReturnsAsync(new PollInfo { Code = "p", IsActive = true, CreatorId = owner });

        var result = await _sut.TogglePinAsync("p", q.Id, userId: owner, isAdmin: false);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsPinned);
    }

    [Fact]
    public async Task TogglePin_ReturnsForbidden_WhenNotOwnerNorAdmin()
    {
        var q = new AudienceQuestion { PollCode = "p", Text = "Q", IsPinned = false };
        _repo.Setup(r => r.GetByIdAsync(q.Id)).ReturnsAsync(q);
        _pollClient.Setup(c => c.GetPollAsync("p"))
            .ReturnsAsync(new PollInfo { Code = "p", IsActive = true, CreatorId = Guid.NewGuid() });

        var result = await _sut.TogglePinAsync("p", q.Id, userId: Guid.NewGuid(), isAdmin: false);

        Assert.False(result.IsSuccess);
        Assert.Contains("forbidden", result.Error!, StringComparison.OrdinalIgnoreCase);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<AudienceQuestion>()), Times.Never);
    }

    [Fact]
    public async Task GetForPoll_ReturnsQuestions()
    {
        _repo.Setup(r => r.GetByPollAsync("p"))
            .ReturnsAsync(new List<AudienceQuestion> { new() { PollCode = "p", Text = "Q1" } });

        var result = await _sut.GetForPollAsync("p");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
    }
}
