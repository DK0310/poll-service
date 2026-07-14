using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace IdentityApi.Tests.Integration;

public class AuthEndpointTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly HttpClient _client;
    public AuthEndpointTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static object Creds(string email, string password = "password1") => new { email, password };
    private static string Unique() => $"user-{Guid.NewGuid():N}@test.com";

    // Registers then verifies via the OTP captured by the fake email sender.
    private async Task RegisterVerified(string email, string password = "password1")
    {
        await _client.PostAsJsonAsync("/api/auth/register", Creds(email, password));
        var code = _factory.Email.CodeFor(email);
        await _client.PostAsJsonAsync("/api/auth/verify-email", new { email, code });
    }

    [Fact]
    public async Task Register_Returns200_RequiresVerification_NoToken()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/register", Creds(Unique()));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("requiresVerification").GetBoolean());
        Assert.False(body.TryGetProperty("token", out _));
    }

    [Fact]
    public async Task VerifyEmail_Returns200_WithToken()
    {
        var email = Unique();
        await _client.PostAsJsonAsync("/api/auth/register", Creds(email));
        var code = _factory.Email.CodeFor(email);

        var res = await _client.PostAsJsonAsync("/api/auth/verify-email", new { email, code });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString()!;
        Assert.Equal(3, token.Split('.').Length);
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
    public async Task Login_Returns200_AfterVerify()
    {
        var email = Unique();
        await RegisterVerified(email, "secret123");

        var res = await _client.PostAsJsonAsync("/api/auth/login", Creds(email, "secret123"));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Login_Returns400_BeforeVerify()
    {
        var email = Unique();
        await _client.PostAsJsonAsync("/api/auth/register", Creds(email, "secret123"));

        var res = await _client.PostAsJsonAsync("/api/auth/login", Creds(email, "secret123"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Login_Returns400_OnWrongPassword()
    {
        var email = Unique();
        await RegisterVerified(email, "secret123");

        var res = await _client.PostAsJsonAsync("/api/auth/login", Creds(email, "wrong-password"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Google_Returns200_WithTokenAndHasPasswordFalse()
    {
        var email = Unique();

        var res = await _client.PostAsJsonAsync("/api/auth/google", new { idToken = $"valid:{email}" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, body.GetProperty("token").GetString()!.Split('.').Length);
        Assert.False(body.GetProperty("hasPassword").GetBoolean());
    }

    [Fact]
    public async Task Google_Returns400_OnInvalidToken()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/google", new { idToken = "garbage" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_Returns200_EvenForUnknownEmail()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Unique() });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task SetPassword_Returns401_WithoutUserHeader()
    {
        // The Gateway would normally set X-User-Id; hitting the service directly without it → 401.
        var res = await _client.PostAsJsonAsync("/api/auth/set-password", new { password = "newpass1" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_ChangesPassword_EndToEnd()
    {
        var email = Unique();
        await RegisterVerified(email, "oldpass1");

        await _client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        var code = _factory.Email.CodeFor(email);
        var reset = await _client.PostAsJsonAsync("/api/auth/reset-password", new { email, code, newPassword = "brandnew1" });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var login = await _client.PostAsJsonAsync("/api/auth/login", Creds(email, "brandnew1"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }
}
