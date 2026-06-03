using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace IdentityApi.Tests.Integration;

public class AuthEndpointTests : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _client;
    public AuthEndpointTests(CustomWebAppFactory factory) => _client = factory.CreateClient();

    private static object Creds(string email, string password = "password1") => new { email, password };
    private static string Unique() => $"user-{Guid.NewGuid():N}@test.com";

    [Fact]
    public async Task Register_Returns200_WithToken()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/register", Creds(Unique()));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString()!;
        Assert.Equal(3, token.Split('.').Length); // header.payload.signature
    }

    [Fact]
    public async Task Register_Returns400_OnDuplicateEmail()
    {
        var email = Unique();
        await _client.PostAsJsonAsync("/api/auth/register", Creds(email));

        var res = await _client.PostAsJsonAsync("/api/auth/register", Creds(email));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Register_Returns400_OnShortPassword()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/register", Creds(Unique(), "123"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Login_Returns200_AfterRegister()
    {
        var email = Unique();
        await _client.PostAsJsonAsync("/api/auth/register", Creds(email, "secret123"));

        var res = await _client.PostAsJsonAsync("/api/auth/login", Creds(email, "secret123"));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Login_Returns400_OnWrongPassword()
    {
        var email = Unique();
        await _client.PostAsJsonAsync("/api/auth/register", Creds(email, "secret123"));

        var res = await _client.PostAsJsonAsync("/api/auth/login", Creds(email, "wrong-password"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
