using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IdentityApi.Common;
using IdentityApi.Data;
using IdentityApi.DTOs;
using IdentityApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace IdentityApi.Services;

public class AuthService
{
    private const int MinPasswordLength = 6;
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    private readonly IdentityDbContext _db;
    private readonly IConfiguration _config;
    private readonly IEmailSender _email;
    private readonly IGoogleTokenVerifier _google;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IdentityDbContext db,
        IConfiguration config,
        IEmailSender email,
        IGoogleTokenVerifier google,
        ILogger<AuthService> logger)
    {
        _db = db;
        _config = config;
        _email = email;
        _google = google;
        _logger = logger;
    }

    // Register with email + password. The account can't log in until the emailed OTP is verified.
    public async Task<Result<RegisterResponse>> RegisterAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return Result<RegisterResponse>.Failure("Email is required");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < MinPasswordLength)
            return Result<RegisterResponse>.Failure($"Password must be at least {MinPasswordLength} characters");

        var email = Normalize(request.Email);
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Result<RegisterResponse>.Failure("Email already registered");

        var user = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            EmailVerified = false
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await IssueAndSendCodeAsync(email, OtpPurpose.EmailVerification);
        return Result<RegisterResponse>.Success(new RegisterResponse());
    }

    public async Task<Result<string>> LoginAsync(LoginRequest request)
    {
        var email = Normalize(request.Email ?? string.Empty);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        // NOTE: same message whether the account is missing or the password is wrong, so an
        // attacker can't probe which emails are registered (account enumeration).
        if (user is null)
            return Result<string>.Failure("Invalid email or password");
        if (user.PasswordHash is null)
            return Result<string>.Failure("This account has no password set. Sign in with Google, or use \"Forgot password\" to set one.");
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Result<string>.Failure("Invalid email or password");
        if (!user.EmailVerified)
            return Result<string>.Failure("Please verify your email before signing in.");

        return Result<string>.Success(GenerateToken(user));
    }

    // Verify the emailed OTP: marks the account usable and returns a login token.
    public async Task<Result<string>> VerifyEmailAsync(VerifyEmailRequest request)
    {
        var email = Normalize(request.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null)
            return Result<string>.Failure("Invalid or expired code");

        var code = await ConsumeCodeAsync(email, OtpPurpose.EmailVerification, request.Code);
        if (!code.IsSuccess)
            return Result<string>.Failure(code.Error!);

        user.EmailVerified = true;
        await _db.SaveChangesAsync();
        return Result<string>.Success(GenerateToken(user));
    }

    public async Task<Result<bool>> ResendCodeAsync(ResendCodeRequest request)
    {
        var purpose = request.Purpose;
        if (purpose != OtpPurpose.EmailVerification && purpose != OtpPurpose.PasswordReset)
            return Result<bool>.Failure("Invalid purpose");

        var email = Normalize(request.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        // Always return success (no enumeration); only actually send when it makes sense and the
        // per-email cooldown has passed.
        var eligible = user is not null &&
            (purpose == OtpPurpose.PasswordReset || !user.EmailVerified);
        if (eligible && !await OnCooldownAsync(email, purpose))
            await IssueAndSendCodeAsync(email, purpose);

        return Result<bool>.Success(true);
    }

    // Google sign-in: verify the ID token, then find-or-create the user and issue our own token.
    public async Task<Result<GoogleAuthResponse>> GoogleAsync(GoogleLoginRequest request)
    {
        var gUser = await _google.VerifyAsync(request.IdToken);
        if (gUser is null)
            return Result<GoogleAuthResponse>.Failure("Invalid Google token");
        if (!gUser.EmailVerified)
            return Result<GoogleAuthResponse>.Failure("Google account email is not verified");

        var email = Normalize(gUser.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.GoogleId == gUser.Subject)
                   ?? await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null)
        {
            user = new User
            {
                Email = email,
                GoogleId = gUser.Subject,
                EmailVerified = true,
                PasswordHash = null
            };
            _db.Users.Add(user);
        }
        else
        {
            user.GoogleId = gUser.Subject;   // link on first Google sign-in for an email account
            user.EmailVerified = true;
        }
        await _db.SaveChangesAsync();

        return Result<GoogleAuthResponse>.Success(new GoogleAuthResponse
        {
            Token = GenerateToken(user),
            HasPassword = user.PasswordHash is not null
        });
    }

    // Give a passwordless (Google) account a password so it can also log in with email + password.
    public async Task<Result<bool>> SetPasswordAsync(Guid userId, SetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < MinPasswordLength)
            return Result<bool>.Failure($"Password must be at least {MinPasswordLength} characters");

        var user = await _db.Users.FindAsync(userId);
        if (user is null)
            return Result<bool>.Failure("User not found");
        if (user.PasswordHash is not null)
            return Result<bool>.Failure("Password already set");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        await _db.SaveChangesAsync();
        return Result<bool>.Success(true);
    }

    // Email an OTP to the logged-in user's own address so they can confirm a password change.
    public async Task<Result<bool>> RequestPasswordChangeCodeAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null)
            return Result<bool>.Failure("User not found");
        // Only a real change needs a code; a first-time set (no password yet) does not.
        if (user.PasswordHash is null)
            return Result<bool>.Failure("No password set to change");

        if (!await OnCooldownAsync(user.Email, OtpPurpose.PasswordChange))
            await IssueAndSendCodeAsync(user.Email, OtpPurpose.PasswordChange);

        return Result<bool>.Success(true);
    }

    // Change password from the profile page. If a password already exists, both the current
    // password and an emailed OTP must check out; a Google account setting one for the first
    // time needs neither.
    public async Task<Result<bool>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < MinPasswordLength)
            return Result<bool>.Failure($"Password must be at least {MinPasswordLength} characters");

        var user = await _db.Users.FindAsync(userId);
        if (user is null)
            return Result<bool>.Failure("User not found");

        if (user.PasswordHash is not null)
        {
            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
                return Result<bool>.Failure("Current password is incorrect");

            var code = await ConsumeCodeAsync(user.Email, OtpPurpose.PasswordChange, request.Code);
            if (!code.IsSuccess)
                return Result<bool>.Failure(code.Error!);
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _db.SaveChangesAsync();
        return Result<bool>.Success(true);
    }

    // Forgot password: email a reset OTP.
    public async Task<Result<bool>> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var email = Normalize(request.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        // NOTE: always returns success even for an unknown email (no account enumeration); the OTP
        // is only actually sent when the account exists and the cooldown has passed.
        if (user is not null && !await OnCooldownAsync(email, OtpPurpose.PasswordReset))
            await IssueAndSendCodeAsync(email, OtpPurpose.PasswordReset);

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ResetPasswordAsync(ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < MinPasswordLength)
            return Result<bool>.Failure($"Password must be at least {MinPasswordLength} characters");

        var email = Normalize(request.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null)
            return Result<bool>.Failure("Invalid or expired code");

        var code = await ConsumeCodeAsync(email, OtpPurpose.PasswordReset, request.Code);
        if (!code.IsSuccess)
            return Result<bool>.Failure(code.Error!);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.EmailVerified = true;   // proving control of the inbox also verifies the email
        await _db.SaveChangesAsync();
        return Result<bool>.Success(true);
    }

    // Helpers
    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private static string GenerateCode() =>
        System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private async Task<bool> OnCooldownAsync(string email, string purpose)
    {
        var since = DateTime.UtcNow - ResendCooldown;
        return await _db.VerificationCodes
            .AnyAsync(v => v.Email == email && v.Purpose == purpose && v.CreatedAt > since);
    }

    private async Task IssueAndSendCodeAsync(string email, string purpose)
    {
        var code = GenerateCode();
        _db.VerificationCodes.Add(new VerificationCode
        {
            Email = email,
            // NOTE: the OTP is hashed (like a password), never stored in plaintext. Verification
            // re-hashes the submitted code and compares, so a DB leak doesn't expose live codes.
            CodeHash = BCrypt.Net.BCrypt.HashPassword(code),
            Purpose = purpose,
            ExpiresAt = DateTime.UtcNow.Add(CodeLifetime)
        });
        await _db.SaveChangesAsync();

        var (subject, body) = purpose == OtpPurpose.PasswordReset
            ? ("Reset your Poll Builder password",
               $"Your password reset code is {code}. It expires in 10 minutes.\n\nIf you didn't request this, you can ignore this email.")
            : ("Verify your Poll Builder email",
               $"Your verification code is {code}. It expires in 10 minutes.");

        // Don't let a mail failure become a 500 or reveal which emails exist. The code is already
        // saved, so the user can just hit "resend".
        try
        {
            await _email.SendAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send {Purpose} email to {Email}", purpose, email);
        }
    }

    private async Task<Result<bool>> ConsumeCodeAsync(string email, string purpose, string code)
    {
        var candidate = await _db.VerificationCodes
            .Where(v => v.Email == email && v.Purpose == purpose
                        && v.ConsumedAt == null && v.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync();

        if (candidate is null || !BCrypt.Net.BCrypt.Verify(code, candidate.CodeHash))
            return Result<bool>.Failure("Invalid or expired code");

        candidate.ConsumedAt = DateTime.UtcNow;
        // Saved by the caller together with the user change.
        return Result<bool>.Success(true);
    }

    private string GenerateToken(User user)
    {
        // NOTE: signed with Jwt:Secret using HMAC-SHA256. The gateway verifies with the SAME secret,
        // so the two configs must match exactly. The "role" claim is what the gateway's admin policy
        // and the downstream X-User-Role header are built from.
        var secret = _config["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // NOTE: 7-day lifetime with no server-side revocation. A jti is included but not tracked,
        // so logging out or changing a password does NOT invalidate tokens already issued.
        var token = new JwtSecurityToken(
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("role", user.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            expires: DateTime.UtcNow.Add(TokenLifetime),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
