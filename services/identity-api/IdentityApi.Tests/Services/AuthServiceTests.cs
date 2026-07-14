using System.IdentityModel.Tokens.Jwt;
using IdentityApi.Data;
using IdentityApi.DTOs;
using IdentityApi.Models;
using IdentityApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace IdentityApi.Tests.Services;

public class AuthServiceTests
{
    // AuthService uses the DbContext directly (no repository), so we test it against
    // an in-memory database + in-memory configuration (real BCrypt + real JWT generation).
    // Email + Google are faked so no external calls happen.
    private static (AuthService sut, IdentityDbContext db, FakeEmailSender email) CreateSutWithDb()
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
        var email = new FakeEmailSender();
        var sut = new AuthService(db, config, email, new FakeGoogleTokenVerifier(),
            NullLogger<AuthService>.Instance);
        return (sut, db, email);
    }

    private static AuthService CreateSut() => CreateSutWithDb().sut;

    private static RegisterRequest Reg(string email = "a@b.com", string pw = "password1") =>
        new() { Email = email, Password = pw };

    // Registers + verifies the email so the account is immediately usable (login-ready).
    private static async Task RegisterVerified(AuthService sut, FakeEmailSender email, string addr, string pw)
    {
        await sut.RegisterAsync(new RegisterRequest { Email = addr, Password = pw });
        await sut.VerifyEmailAsync(new VerifyEmailRequest { Email = addr, Code = email.CodeFor(addr) });
    }

    // ── Register ────────────────────────────────────────────────

    [Fact]
    public async Task Register_Succeeds_ButReturnsNoTokenUntilVerified()
    {
        var (sut, db, email) = CreateSutWithDb();

        var result = await sut.RegisterAsync(Reg());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.RequiresVerification);
        var user = await db.Users.SingleAsync();
        Assert.False(user.EmailVerified);
        Assert.NotEqual("", email.CodeFor("a@b.com")); // a code was emailed
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

    // ── Verify email ────────────────────────────────────────────

    [Fact]
    public async Task VerifyEmail_ReturnsJwt_AndMarksVerified()
    {
        var (sut, db, email) = CreateSutWithDb();
        await sut.RegisterAsync(Reg("v@b.com"));

        var result = await sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "v@b.com", Code = email.CodeFor("v@b.com") });

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Split('.').Length);
        Assert.True((await db.Users.SingleAsync()).EmailVerified);
    }

    [Fact]
    public async Task VerifyEmail_Fails_OnWrongCode()
    {
        var sut = CreateSut();
        await sut.RegisterAsync(Reg("v2@b.com"));

        var result = await sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "v2@b.com", Code = "000000" });

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task VerifyEmail_Fails_WhenCodeExpired()
    {
        var (sut, db, email) = CreateSutWithDb();
        await sut.RegisterAsync(Reg("v3@b.com"));
        var code = await db.VerificationCodes.SingleAsync();
        code.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var result = await sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "v3@b.com", Code = email.CodeFor("v3@b.com") });

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task VerifyEmail_Code_IsSingleUse()
    {
        var (sut, _, email) = CreateSutWithDb();
        await sut.RegisterAsync(Reg("v4@b.com"));
        var code = email.CodeFor("v4@b.com");
        await sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "v4@b.com", Code = code });

        var again = await sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "v4@b.com", Code = code });

        Assert.False(again.IsSuccess);
    }

    // ── Login ───────────────────────────────────────────────────

    [Fact]
    public async Task Login_ReturnsToken_WhenVerified()
    {
        var (sut, _, email) = CreateSutWithDb();
        await RegisterVerified(sut, email, "login@b.com", "secret123");

        var result = await sut.LoginAsync(new LoginRequest { Email = "login@b.com", Password = "secret123" });

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Value));
    }

    [Fact]
    public async Task Login_Blocked_WhenEmailNotVerified()
    {
        var sut = CreateSut();
        await sut.RegisterAsync(Reg("unverified@b.com", "secret123"));

        var result = await sut.LoginAsync(new LoginRequest { Email = "unverified@b.com", Password = "secret123" });

        Assert.False(result.IsSuccess);
        Assert.Contains("verify", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_TokenHasRoleAdmin_WhenUserIsAdmin()
    {
        var (sut, db, email) = CreateSutWithDb();
        await RegisterVerified(sut, email, "admin@b.com", "secret123");
        var user = await db.Users.FirstAsync(u => u.Email == "admin@b.com");
        user.Role = "Admin";
        await db.SaveChangesAsync();

        var result = await sut.LoginAsync(new LoginRequest { Email = "admin@b.com", Password = "secret123" });

        Assert.True(result.IsSuccess);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Value!);
        Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == "Admin");
    }

    [Fact]
    public async Task Login_ReturnsFailure_WhenPasswordWrong()
    {
        var (sut, _, email) = CreateSutWithDb();
        await RegisterVerified(sut, email, "login2@b.com", "secret123");

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

    [Fact]
    public async Task Login_ReturnsFailure_ForGoogleOnlyAccount()
    {
        var sut = CreateSut();
        await sut.GoogleAsync(new GoogleLoginRequest { IdToken = "valid:g@b.com" });

        var result = await sut.LoginAsync(new LoginRequest { Email = "g@b.com", Password = "whatever1" });

        Assert.False(result.IsSuccess);
        Assert.Contains("Google", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    // ── Google sign-in ──────────────────────────────────────────

    [Fact]
    public async Task Google_CreatesVerifiedUser_WithoutPassword()
    {
        var (sut, db, _) = CreateSutWithDb();

        var result = await sut.GoogleAsync(new GoogleLoginRequest { IdToken = "valid:new@b.com" });

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.HasPassword);
        var user = await db.Users.SingleAsync();
        Assert.True(user.EmailVerified);
        Assert.Null(user.PasswordHash);
        Assert.Equal("google-new@b.com", user.GoogleId);
    }

    [Fact]
    public async Task Google_Fails_OnInvalidToken()
    {
        var sut = CreateSut();

        var result = await sut.GoogleAsync(new GoogleLoginRequest { IdToken = "garbage" });

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Google_LinksToExistingEmailAccount()
    {
        var (sut, db, email) = CreateSutWithDb();
        await RegisterVerified(sut, email, "both@b.com", "secret123");

        var result = await sut.GoogleAsync(new GoogleLoginRequest { IdToken = "valid:both@b.com" });

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.HasPassword); // the existing password is retained
        var user = await db.Users.SingleAsync();
        Assert.Equal("google-both@b.com", user.GoogleId);
    }

    // ── Set password (dual login) ───────────────────────────────

    [Fact]
    public async Task SetPassword_EnablesEmailPasswordLogin_ForGoogleUser()
    {
        var (sut, db, _) = CreateSutWithDb();
        await sut.GoogleAsync(new GoogleLoginRequest { IdToken = "valid:sp@b.com" });
        var userId = (await db.Users.SingleAsync()).Id;

        var set = await sut.SetPasswordAsync(userId, new SetPasswordRequest { Password = "newpass1" });
        Assert.True(set.IsSuccess);

        var login = await sut.LoginAsync(new LoginRequest { Email = "sp@b.com", Password = "newpass1" });
        Assert.True(login.IsSuccess);
    }

    [Fact]
    public async Task SetPassword_Fails_WhenPasswordAlreadySet()
    {
        var (sut, db, email) = CreateSutWithDb();
        await RegisterVerified(sut, email, "has@b.com", "secret123");
        var userId = (await db.Users.SingleAsync()).Id;

        var result = await sut.SetPasswordAsync(userId, new SetPasswordRequest { Password = "another1" });

        Assert.False(result.IsSuccess);
    }

    // ── Password reset ──────────────────────────────────────────

    [Fact]
    public async Task ForgotThenReset_ChangesPassword()
    {
        var (sut, _, email) = CreateSutWithDb();
        await RegisterVerified(sut, email, "reset@b.com", "oldpass1");

        await sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "reset@b.com" });
        var code = email.CodeFor("reset@b.com");
        var reset = await sut.ResetPasswordAsync(new ResetPasswordRequest { Email = "reset@b.com", Code = code, NewPassword = "brandnew1" });
        Assert.True(reset.IsSuccess);

        Assert.False((await sut.LoginAsync(new LoginRequest { Email = "reset@b.com", Password = "oldpass1" })).IsSuccess);
        Assert.True((await sut.LoginAsync(new LoginRequest { Email = "reset@b.com", Password = "brandnew1" })).IsSuccess);
    }

    [Fact]
    public async Task Reset_Fails_OnWrongCode()
    {
        var (sut, _, email) = CreateSutWithDb();
        await RegisterVerified(sut, email, "reset2@b.com", "oldpass1");
        await sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "reset2@b.com" });

        var reset = await sut.ResetPasswordAsync(new ResetPasswordRequest { Email = "reset2@b.com", Code = "000000", NewPassword = "brandnew1" });

        Assert.False(reset.IsSuccess);
    }

    [Fact]
    public async Task Forgot_IsAlways200_ForUnknownEmail_AndSendsNothing()
    {
        var (sut, _, email) = CreateSutWithDb();

        var result = await sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "nobody@b.com" });

        Assert.True(result.IsSuccess);
        Assert.Equal("", email.CodeFor("nobody@b.com"));
    }

    // ── Change password (from profile) ──────────────────────────

    [Fact]
    public async Task ChangePassword_Succeeds_WithCorrectCurrent()
    {
        var (sut, db, email) = CreateSutWithDb();
        await RegisterVerified(sut, email, "cp@b.com", "oldpass1");
        var id = (await db.Users.SingleAsync()).Id;

        var result = await sut.ChangePasswordAsync(id, new ChangePasswordRequest { CurrentPassword = "oldpass1", NewPassword = "newpass1" });

        Assert.True(result.IsSuccess);
        Assert.True((await sut.LoginAsync(new LoginRequest { Email = "cp@b.com", Password = "newpass1" })).IsSuccess);
    }

    [Fact]
    public async Task ChangePassword_Fails_WithWrongCurrent()
    {
        var (sut, db, email) = CreateSutWithDb();
        await RegisterVerified(sut, email, "cp2@b.com", "oldpass1");
        var id = (await db.Users.SingleAsync()).Id;

        var result = await sut.ChangePasswordAsync(id, new ChangePasswordRequest { CurrentPassword = "wrongpass", NewPassword = "newpass1" });

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ChangePassword_SetsFirstPassword_ForGoogleUser_WithoutCurrent()
    {
        var (sut, db, _) = CreateSutWithDb();
        await sut.GoogleAsync(new GoogleLoginRequest { IdToken = "valid:cp3@b.com" });
        var id = (await db.Users.SingleAsync()).Id;

        var result = await sut.ChangePasswordAsync(id, new ChangePasswordRequest { CurrentPassword = "", NewPassword = "newpass1" });

        Assert.True(result.IsSuccess);
        Assert.True((await sut.LoginAsync(new LoginRequest { Email = "cp3@b.com", Password = "newpass1" })).IsSuccess);
    }
}
