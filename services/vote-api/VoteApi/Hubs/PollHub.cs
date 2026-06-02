using Microsoft.AspNetCore.SignalR;

namespace VoteApi.Hubs;

/// <summary>
/// SignalR hub for live results. Clients join a per-poll group keyed by the poll code,
/// then receive "ReceiveVoteUpdate" broadcasts whenever a vote is recorded for that poll.
/// The broadcast itself is performed by VoteService via IHubContext (not from this class).
/// </summary>
public class PollHub : Hub
{
    public Task JoinPollGroup(string pollCode)
        => Groups.AddToGroupAsync(Context.ConnectionId, pollCode);

    public Task LeavePollGroup(string pollCode)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, pollCode);
}
