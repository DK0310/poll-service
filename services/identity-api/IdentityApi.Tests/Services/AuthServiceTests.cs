using System.IdentityModel.Tokens.Jwt;
using IdentityApi.Data;
using IdentityApi.DTOs;
using IdentityApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IdentityApi.Tests.Services;

public class AuthServiceTests
{
    // AuthService uses the DbContext directly (no repository), so we test it against
    // an in-memory database + in-memory configuration (real BCrypt + real JWT generation).
    private static AuthService CreateSut() => CreateSutWithDb().sut;

    private static (AuthService sut, IdentityDbContext db) CreateSutWithDb()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase("identity_" + Guid.NewGuid())
            .Options;
        var db = new IdentityDbContext(options);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-secret-key-that-is-at-least-32-characters-long!"
            })
            .Build();
        return (new AuthService(db, config), db);
    }

    private static RegisterRequest Reg(string email = "a@b.com", string pw = "password1") =>
        new() { Email = email, Password = pw };

    // ── Register ────────────────────────────────────────────────

    [Fact]
    public async Task Register_ReturnsValidJwt_WhenValid()
    {
        var sut = CreateSut();

        var result = await sut.RegisterAsync(Reg());

        Assert.True(result.IsSuccess);
        var token = result.Value!;
        Assert.Equal(3, token.Split('.').Length); // header.payload.signature
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Sub);
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "a@b.com");
    }

    [Fact]
    public async Task Register_TokenHasRoleUser_ByDefault()
    {
        var sut = CreateSut();

        var result = await sut.RegisterAsync(Reg());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Value!);
        Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == "User");
    }

    [Fact]
    public async Task Login_TokenHasRoleAdmin_WhenUserIsAdmin()
    {
        var (sut, db) = CreateSutWithDb();
        await sut.RegisterAsync(Reg("admin@b.com", "secret123"));
        var user = await db.Users.FirstAsync(u => u.Email == "admin@b.com");
        user.Role = "Admin";
        await db.SaveChangesAsync();

        var result = await sut.LoginAsync(new LoginRequest { Email = "admin@b.com", Password = "secret123" });

        Assert.True(result.IsSuccess);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Value!);
        Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == "Admin");
    }

    [Fact]
    public async Task Register_ReturnsFailure_WhenEmailEmpty()
    {
        var sut = CreateSut();

        var result = await sut.RegisterAsync(Reg(email: ""));

        Assert.False(result.IsSuccess);
        Assert.Contains("Email", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_ReturnsFailure_WhenPasswordTooShort()
    {
        var sut = CreateSut();

        var result = await sut.RegisterAsync(Reg(pw: "123"));

        Assert.False(result.IsSuccess);
        Assert.Contains("at least", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_ReturnsFailure_WhenDuplicateEmail()
    {
        var sut = CreateSut();
        await sut.RegisterAsync(Reg("dupe@b.com"));

        var result = await sut.RegisterAsync(Reg("dupe@b.com"));

        Assert.False(result.IsSuccess);
        Assert.Contains("already registered", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_IsCaseInsensitiveOnEmail()
    {
        var sut = CreateSut();
        await sut.RegisterAsync(Reg("Mixed@Case.com"));

        var result = await sut.RegisterAsync(Reg("mixed@case.com"));

        Assert.False(result.IsSuccess); // treated as duplicate
    }

    // ── Login ───────────────────────────────────────────────────

    [Fact]
    public async Task Login_ReturnsToken_WhenCredentialsValid()
    {
        var sut = CreateSut();
        await sut.RegisterAsync(Reg("login@b.com", "secret123"));

        var result = await sut.LoginAsync(new LoginRequest { Email = "login@b.com", Password = "secret123" });

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Value));
    }

    [Fact]
    public async Task Login_ReturnsFailure_WhenPasswordWrong()
    {
        var sut = CreateSut();
        await sut.RegisterAsync(Reg("login2@b.com", "secret123"));

        var result = await sut.LoginAsync(new LoginRequest { Email = "login2@b.com", Password = "wrongpass" });

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_ReturnsFailure_WhenUserNotFound()
    {
        var sut = CreateSut();

        var result = await sut.LoginAsync(new LoginRequest { Email = "ghost@b.com", Password = "whatever1" });

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid", result.Error!, StringComparison.OrdinalIgnoreCase);
    }
}
