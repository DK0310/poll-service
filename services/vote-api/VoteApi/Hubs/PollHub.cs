using Microsoft.AspNetCore.SignalR;

namespace VoteApi.Hubs;

/// <summary>
/// SignalR hub for live results. A client joins the group named after a poll code and then
/// receives "ReceiveVoteUpdate" / "ReceiveAskUpdate" pushes for that poll. The pushes are sent
/// from the services (VoteService/AskService) via IHubContext, not from here.
/// </summary>
public class PollHub : Hub
{
    // NOTE: no authorization. Anyone who knows a poll code can join its group and watch results
    // live. That's fine while every poll is public; add an ownership/visibility check here first
    // if private polls are ever introduced.
    public Task JoinPollGroup(string pollCode)
        => Groups.AddToGroupAsync(Context.ConnectionId, pollCode);

    public Task LeavePollGroup(string pollCode)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, pollCode);
}
