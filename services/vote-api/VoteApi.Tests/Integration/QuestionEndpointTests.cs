using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace VoteApi.Tests.Integration;

public class QuestionEndpointTests : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _client;
    public QuestionEndpointTests(CustomWebAppFactory factory) => _client = factory.CreateClient();

    private async Task<string> SubmitAsync(string code, string text)
    {
        var res = await _client.PostAsJsonAsync($"/api/polls/{code}/questions", new { text });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task Submit_Returns200_WithQuestion()
    {
        var res = await _client.PostAsJsonAsync("/api/polls/qa1/questions", new { text = "Is this live?" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Is this live?", body.GetProperty("text").GetString());
        Assert.Equal(0, body.GetProperty("upvotes").GetInt32());
    }

    [Fact]
    public async Task List_Returns200_AfterSubmit()
    {
        await SubmitAsync("qa2", "First question");

        var res = await _client.GetAsync("/api/polls/qa2/questions");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task Upvote_Returns200_AndIncrements()
    {
        var id = await SubmitAsync("qa3", "Upvote me");

        var res = await _client.PostAsync($"/api/polls/qa3/questions/{id}/upvote", content: null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("upvotes").GetInt32());
    }

    [Fact]
    public async Task Pin_Returns200_AndTogglesPinned()
    {
        var id = await SubmitAsync("qa4", "Pin me");

        var res = await _client.PostAsync($"/api/polls/qa4/questions/{id}/pin", content: null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("isPinned").GetBoolean());
    }

    [Fact]
    public async Task Submit_Returns404_WhenPollMissing()
    {
        var res = await _client.PostAsJsonAsync("/api/polls/nope1/questions", new { text = "x" });

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
