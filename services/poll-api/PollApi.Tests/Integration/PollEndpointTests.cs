using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PollApi.Tests.Integration;

public class PollEndpointTests : IClassFixture<CustomWebAppFactory>
{
    // Stand-in for the X-User-Id the Gateway sets after JWT validation.
    private const string OwnerId = "11111111-1111-1111-1111-111111111111";

    private readonly HttpClient _client;
    public PollEndpointTests(CustomWebAppFactory factory) => _client = factory.CreateClient();

    // Posts a create request, optionally as a logged-in user (X-User-Id) and/or admin (X-User-Role).
    private async Task<HttpResponseMessage> PostCreateAsync(object body, string? userId = OwnerId)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/polls") { Content = JsonContent.Create(body) };
        if (userId is not null) req.Headers.Add("X-User-Id", userId);
        return await _client.SendAsync(req);
    }

    private async Task<string> CreatePollAsync(string question = "Favorite language?")
    {
        var res = await PostCreateAsync(new { question, options = new[] { "C#", "TypeScript", "Python" } });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task CreatePoll_Returns201_WhenValid()
    {
        var res = await PostCreateAsync(new
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
    public async Task CreatePoll_Returns401_WithoutUserHeader()
    {
        // Creating now requires a logged-in user (Gateway enforces; no X-User-Id here).
        var res = await PostCreateAsync(new
        {
            question = "Anon?",
            options = new[] { "A", "B" }
        }, userId: null);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task CreatePoll_Returns400_WhenTooFewOptions()
    {
        var res = await PostCreateAsync(new
        {
            question = "Only one?",
            options = new[] { "Solo" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task AdminPolls_Returns403_WithoutAdminRole()
    {
        var res = await _client.GetAsync("/api/admin/polls");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task AdminPolls_Returns200_WithAdminRole()
    {
        await CreatePollAsync();
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/admin/polls");
        req.Headers.Add("X-User-Role", "Admin");

        var res = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Close_ByAdmin_Returns200_EvenWhenNotCreator()
    {
        var code = await CreatePollAsync(); // owner = OwnerId
        var req = new HttpRequestMessage(HttpMethod.Patch, $"/api/polls/{code}/close");
        req.Headers.Add("X-User-Id", "99999999-9999-9999-9999-999999999999"); // a different user
        req.Headers.Add("X-User-Role", "Admin");

        var res = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
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
