using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace IdentityApi.Tests.Integration;

public class AdminUsersEndpointTests : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _client;
    public AdminUsersEndpointTests(CustomWebAppFactory factory) => _client = factory.CreateClient();

    private async Task RegisterAsync(string email)
    {
        var res = await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "secret123" });
        res.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage AsAdmin(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-User-Role", "Admin");
        req.Headers.Add("X-User-Id", "11111111-1111-1111-1111-111111111111"); // a different admin
        return req;
    }

    [Fact]
    public async Task ListUsers_Returns403_WithoutAdminRole()
    {
        var res = await _client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task ListUsers_Returns200_WithAdminRole()
    {
        await RegisterAsync("listed@b.com");

        var res = await _client.SendAsync(AsAdmin(HttpMethod.Get, "/api/admin/users"));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task SetRole_PromotesUserToAdmin()
    {
        await RegisterAsync("promote@b.com");
        var list = await _client.SendAsync(AsAdmin(HttpMethod.Get, "/api/admin/users"));
        var users = await list.Content.ReadFromJsonAsync<JsonElement>();
        var id = users.EnumerateArray()
            .First(u => u.GetProperty("email").GetString() == "promote@b.com")
            .GetProperty("id").GetString();

        var req = AsAdmin(HttpMethod.Post, $"/api/admin/users/{id}/role");
        req.Content = JsonContent.Create(new { role = "Admin" });
        var res = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Admin", body.GetProperty("role").GetString());
    }
}
