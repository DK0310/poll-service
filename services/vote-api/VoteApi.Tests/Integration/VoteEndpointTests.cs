using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace VoteApi.Tests.Integration;

public class VoteEndpointTests : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _client;
    public VoteEndpointTests(CustomWebAppFactory factory) => _client = factory.CreateClient();

    private static object Vote(int optionIndex, string voterToken) => new { optionIndex, voterToken };

    // Each test uses its own poll code so vote tallies stay isolated in the shared in-memory DB.
    // (The fake Poll client treats any code except "nope1"/"closed" as an active 2-option poll.)

    [Fact]
    public async Task Vote_Returns200_AndTallies()
    {
        var res = await _client.PostAsJsonAsync("/api/polls/tally1/vote", Vote(0, "voter-1"));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("totalVotes").GetInt32());
        Assert.Equal(100, body.GetProperty("options")[0].GetProperty("percentage").GetDouble());
    }

    [Fact]
    public async Task Vote_Returns409_OnDuplicate()
    {
        await _client.PostAsJsonAsync("/api/polls/dupe1/vote", Vote(0, "dupe-voter"));

        var res = await _client.PostAsJsonAsync("/api/polls/dupe1/vote", Vote(1, "dupe-voter"));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Vote_Returns404_WhenPollMissing()
    {
        var res = await _client.PostAsJsonAsync("/api/polls/nope1/vote", Vote(0, "v"));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Vote_Returns400_WhenPollClosed()
    {
        var res = await _client.PostAsJsonAsync("/api/polls/closed/vote", Vote(0, "v"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Results_Returns200_WithOptions()
    {
        var res = await _client.GetAsync("/api/polls/res1/results");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("options").GetArrayLength());
    }

    [Fact]
    public async Task Analytics_Returns200_AfterVote_ForOwner()
    {
        await _client.PostAsJsonAsync("/api/polls/ana1/vote", Vote(0, "analytics-voter"));

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/polls/ana1/analytics");
        req.Headers.Add("X-User-Id", FakePollClientService.OwnerId.ToString()); // poll owner

        var res = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("totalVotes").GetInt32() >= 1);
        Assert.True(body.GetProperty("timeline").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task Analytics_Returns403_WhenNotOwnerNorAdmin()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/polls/ana2/analytics");
        req.Headers.Add("X-User-Id", "99999999-9999-9999-9999-999999999999"); // not the owner

        var res = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Analytics_Returns404_WhenMissing()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/polls/nope1/analytics");
        req.Headers.Add("X-User-Role", "Admin"); // admin, but poll doesn't exist → 404

        var res = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
