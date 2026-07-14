using IdentityApi.Data;
using IdentityApi.DTOs;
using IdentityApi.Models;
using IdentityApi.Services;
using Microsoft.EntityFrameworkCore;

namespace IdentityApi.Tests.Services;

public class ProfileServiceTests
{
    private static (ProfileService sut, IdentityDbContext db, User user) CreateSut(bool withPassword = true, bool withGoogle = false)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase("profile_" + Guid.NewGuid())
            .Options;
        var db = new IdentityDbContext(options);
        var user = new User
        {
            Email = "u@b.com",
            PasswordHash = withPassword ? "hash" : null,
            GoogleId = withGoogle ? "google-u@b.com" : null,
            EmailVerified = true
        };
        db.Users.Add(user);
        db.SaveChanges();
        return (new ProfileService(db), db, user);
    }

    [Fact]
    public async Task Get_ReturnsProfile_WithAuthFlags()
    {
        var (sut, _, user) = CreateSut(withPassword: false, withGoogle: true);

        var result = await sut.GetAsync(user.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("u@b.com", result.Value!.Email);
        Assert.False(result.Value.HasPassword);
        Assert.True(result.Value.HasGoogle);
    }

    [Fact]
    public async Task Get_Fails_WhenUserMissing()
    {
        var (sut, _, _) = CreateSut();
        var result = await sut.GetAsync(Guid.NewGuid());
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Update_SetsUsernameBioAvatar()
    {
        var (sut, db, user) = CreateSut();

        var result = await sut.UpdateAsync(user.Id, new UpdateProfileRequest
        {
            Username = "  Neo  ",
            Bio = "  I ask the room.  ",
            AvatarUrl = "data:image/png;base64,AAAA"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("Neo", result.Value!.Username);          // trimmed
        Assert.Equal("I ask the room.", result.Value.Bio);
        var saved = await db.Users.FindAsync(user.Id);
        Assert.Equal("data:image/png;base64,AAAA", saved!.AvatarUrl);
    }

    [Fact]
    public async Task Update_ClearsFields_OnEmptyStrings()
    {
        var (sut, _, user) = CreateSut();
        await sut.UpdateAsync(user.Id, new UpdateProfileRequest { Username = "Neo", Bio = "hi", AvatarUrl = "data:image/png;base64,AAAA" });

        var result = await sut.UpdateAsync(user.Id, new UpdateProfileRequest { Username = "", Bio = "", AvatarUrl = "" });

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Username);
        Assert.Null(result.Value.Bio);
        Assert.Null(result.Value.AvatarUrl);
    }

    [Fact]
    public async Task Update_Rejects_NonImageAvatar()
    {
        var (sut, _, user) = CreateSut();

        var result = await sut.UpdateAsync(user.Id, new UpdateProfileRequest { AvatarUrl = "https://evil.example/x.png" });

        Assert.False(result.IsSuccess);
        Assert.Contains("image", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_Rejects_OversizeAvatar()
    {
        var (sut, _, user) = CreateSut();
        var huge = "data:image/png;base64," + new string('A', 300_001);

        var result = await sut.UpdateAsync(user.Id, new UpdateProfileRequest { AvatarUrl = huge });

        Assert.False(result.IsSuccess);
        Assert.Contains("too large", result.Error!, StringComparison.OrdinalIgnoreCase);
    }
}
