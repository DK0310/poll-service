using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace IdentityApi.Tests.Integration;

public class ProfileEndpointTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly HttpClient _client;
    public ProfileEndpointTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string Unique() => $"user-{Guid.NewGuid():N}@test.com";

    // Registers + verifies, returning (email, userId) so tests can set the Gateway's X-User-Id header.
    private async Task<(string email, string userId)> CreateUser(string password = "password1")
    {
        var email = Unique();
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password });
        var code = _factory.Email.CodeFor(email);
        var res = await _client.PostAsJsonAsync("/api/auth/verify-email", new { email, code });
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString()!;
        var sub = new JwtSecurityTokenHandler().ReadJwtToken(token).Subject;
        return (email, sub);
    }

    private HttpRequestMessage As(string userId, HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-User-Id", userId);
        return req;
    }

    [Fact]
    public async Task Me_Returns401_WithoutUserHeader()
    {
        var res = await _client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsProfile_WithUserHeader()
    {
        var (email, userId) = await CreateUser();

        var res = await _client.SendAsync(As(userId, HttpMethod.Get, "/api/users/me"));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.True(body.GetProperty("hasPassword").GetBoolean());
    }

    [Fact]
    public async Task Update_SavesUsernameAndBio()
    {
        var (_, userId) = await CreateUser();

        var req = As(userId, HttpMethod.Put, "/api/users/me");
        req.Content = JsonContent.Create(new { username = "Roomie", bio = "hi" });
        var res = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Roomie", body.GetProperty("username").GetString());
    }

    [Fact]
    public async Task ChangePassword_Returns401_WithoutUserHeader()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = "password1", newPassword = "newpass1" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_Works_EndToEnd()
    {
        var (email, userId) = await CreateUser("oldpass1");

        // Request the emailed OTP, then change with current password + code.
        var codeReq = await _client.SendAsync(As(userId, HttpMethod.Post, "/api/auth/change-password/request-code"));
        Assert.Equal(HttpStatusCode.OK, codeReq.StatusCode);
        var code = _factory.Email.CodeFor(email);

        var req = As(userId, HttpMethod.Post, "/api/auth/change-password");
        req.Content = JsonContent.Create(new { currentPassword = "oldpass1", newPassword = "brandnew1", code });
        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "brandnew1" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_Fails_WithoutCode()
    {
        var (_, userId) = await CreateUser("oldpass1");

        var req = As(userId, HttpMethod.Post, "/api/auth/change-password");
        req.Content = JsonContent.Create(new { currentPassword = "oldpass1", newPassword = "brandnew1" });
        var res = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
