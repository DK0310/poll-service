using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PollApi.Tests.Integration;

public class PollEndpointTests : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _client;
    public PollEndpointTests(CustomWebAppFactory factory) => _client = factory.CreateClient();

    private async Task<string> CreatePollAsync(string question = "Favorite language?")
    {
        var res = await _client.PostAsJsonAsync("/api/polls", new
        {
            question,
            options = new[] { "C#", "TypeScript", "Python" }
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task CreatePoll_Returns201_WhenValid()
    {
        var res = await _client.PostAsJsonAsync("/api/polls", new
        {
            question = "Favorite language?",
            options = new[] { "C#", "TypeScript", "Python" }
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Favorite language?", body.GetProperty("question").GetString());
        Assert.Equal(5, body.GetProperty("code").GetString()!.Length);
        Assert.Equal(3, body.GetProperty("options").GetArrayLength());
    }

    [Fact]
    public async Task CreatePoll_Returns400_WhenTooFewOptions()
    {
        var res = await _client.PostAsJsonAsync("/api/polls", new
        {
            question = "Only one?",
            options = new[] { "Solo" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task GetPoll_Returns200_WhenExists()
    {
        var code = await CreatePollAsync();

        var res = await _client.GetAsync($"/api/polls/{code}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.True(body.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task GetPoll_Returns404_WhenMissing()
    {
        var res = await _client.GetAsync("/api/polls/ZZZZZ");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task MyPolls_Returns401_WithoutUserHeader()
    {
        // No X-User-Id (the Gateway would set it after JWT validation).
        var res = await _client.GetAsync("/api/polls/my-polls");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Close_Returns401_WithoutUserHeader()
    {
        var code = await CreatePollAsync();

        var res = await _client.PatchAsync($"/api/polls/{code}/close", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
