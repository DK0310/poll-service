using IdentityApi.Common;
using IdentityApi.Data;
using IdentityApi.DTOs;
using Microsoft.EntityFrameworkCore;

namespace IdentityApi.Services;

/// <summary>Admin-only user management (list, promote/demote, delete).</summary>
public class AdminService
{
    private static readonly string[] ValidRoles = ["User", "Admin"];

    private readonly IdentityDbContext _db;
    public AdminService(IdentityDbContext db) => _db = db;

    public async Task<List<AdminUserResponse>> ListUsersAsync()
        => await _db.Users
            .OrderBy(u => u.Email)
            .Select(u => new AdminUserResponse
            {
                Id = u.Id,
                Email = u.Email,
                Role = u.Role,
                CreatedAt = u.CreatedAt,
            })
            .ToListAsync();

    public async Task<Result<AdminUserResponse>> SetRoleAsync(Guid id, string role, Guid? callerId)
    {
        if (!ValidRoles.Contains(role))
            return Result<AdminUserResponse>.Failure("Invalid role");
        if (callerId == id)
            return Result<AdminUserResponse>.Failure("You cannot change your own role");

        var user = await _db.Users.FindAsync(id);
        if (user is null) return Result<AdminUserResponse>.Failure("User not found");

        user.Role = role;
        await _db.SaveChangesAsync();
        return Result<AdminUserResponse>.Success(new AdminUserResponse
        {
            Id = user.Id,
            Email = user.Email,
            Role = user.Role,
            CreatedAt = user.CreatedAt,
        });
    }

    public async Task<Result<bool>> DeleteUserAsync(Guid id, Guid? callerId)
    {
        if (callerId == id)
            return Result<bool>.Failure("You cannot delete your own account");

        var user = await _db.Users.FindAsync(id);
        if (user is null) return Result<bool>.Failure("User not found");

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return Result<bool>.Success(true);
    }
}
