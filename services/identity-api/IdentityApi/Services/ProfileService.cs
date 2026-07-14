using IdentityApi.Common;
using IdentityApi.Data;
using IdentityApi.DTOs;
using IdentityApi.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityApi.Services;

public class ProfileService
{
    // Base64 of a 256px avatar is well under this; the cap guards the DB row + API payload.
    private const int MaxAvatarChars = 300_000;

    private readonly IdentityDbContext _db;
    public ProfileService(IdentityDbContext db) => _db = db;

    public async Task<Result<ProfileResponse>> GetAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        return user is null
            ? Result<ProfileResponse>.Failure("User not found")
            : Result<ProfileResponse>.Success(ToResponse(user));
    }

    public async Task<Result<ProfileResponse>> UpdateAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null)
            return Result<ProfileResponse>.Failure("User not found");

        var username = request.Username?.Trim();
        if (username is { Length: > 50 })
            return Result<ProfileResponse>.Failure("Username must be 50 characters or fewer");

        var bio = request.Bio?.Trim();
        if (bio is { Length: > 300 })
            return Result<ProfileResponse>.Failure("Bio must be 300 characters or fewer");

        var avatar = request.AvatarUrl;
        if (!string.IsNullOrEmpty(avatar))
        {
            if (!avatar.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                return Result<ProfileResponse>.Failure("Avatar must be an image data URL");
            if (avatar.Length > MaxAvatarChars)
                return Result<ProfileResponse>.Failure("Avatar image is too large");
        }

        // Empty strings clear the field back to null.
        user.Username = string.IsNullOrEmpty(username) ? null : username;
        user.Bio = string.IsNullOrEmpty(bio) ? null : bio;
        user.AvatarUrl = string.IsNullOrEmpty(avatar) ? null : avatar;
        await _db.SaveChangesAsync();

        return Result<ProfileResponse>.Success(ToResponse(user));
    }

    private static ProfileResponse ToResponse(User u) => new()
    {
        Id = u.Id,
        Email = u.Email,
        Username = u.Username,
        Bio = u.Bio,
        AvatarUrl = u.AvatarUrl,
        Role = u.Role,
        HasPassword = u.PasswordHash is not null,
        HasGoogle = u.GoogleId is not null,
        CreatedAt = u.CreatedAt
    };
}
