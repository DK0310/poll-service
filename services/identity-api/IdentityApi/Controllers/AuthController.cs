using IdentityApi.DTOs;
using IdentityApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace IdentityApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _service;
    public AuthController(AuthService service) => _service = service;

    // ── POST /api/auth/register ─────────────────────────────────
    // Creates an unverified account and emails an OTP. No token until the email is verified.
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _service.RegisterAsync(request);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    // ── POST /api/auth/verify-email  { email, code } ────────────
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        var result = await _service.VerifyEmailAsync(request);
        return result.IsSuccess
            ? Ok(new AuthResponse { Token = result.Value! })
            : BadRequest(new { error = result.Error });
    }

    // ── POST /api/auth/resend-code  { email, purpose } ──────────
    [HttpPost("resend-code")]
    public async Task<IActionResult> ResendCode([FromBody] ResendCodeRequest request)
    {
        var result = await _service.ResendCodeAsync(request);
        return result.IsSuccess
            ? Ok()
            : BadRequest(new { error = result.Error });
    }

    // ── POST /api/auth/login ────────────────────────────────────
    // Bad credentials return 400 (not 401) so the frontend's global 401 handler
    // doesn't hijack a failed login attempt.
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _service.LoginAsync(request);
        return result.IsSuccess
            ? Ok(new AuthResponse { Token = result.Value! })
            : BadRequest(new { error = result.Error });
    }

    // ── POST /api/auth/google  { idToken } ──────────────────────
    [HttpPost("google")]
    public async Task<IActionResult> Google([FromBody] GoogleLoginRequest request)
    {
        var result = await _service.GoogleAsync(request);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    // ── POST /api/auth/set-password  { password } ───────────────
    // Authenticated at the Gateway (authorization policy); we trust the X-User-Id header it sets.
    [HttpPost("set-password")]
    public async Task<IActionResult> SetPassword([FromBody] SetPasswordRequest request)
    {
        var userId = CallerId();
        if (userId is null)
            return Unauthorized(new { error = "Not authenticated" });

        var result = await _service.SetPasswordAsync(userId.Value, request);
        return result.IsSuccess
            ? Ok()
            : BadRequest(new { error = result.Error });
    }

    // ── POST /api/auth/change-password/request-code ─────────────
    // Authenticated at the Gateway; emails a PasswordChange OTP to the caller's own address.
    [HttpPost("change-password/request-code")]
    public async Task<IActionResult> RequestChangePasswordCode()
    {
        var userId = CallerId();
        if (userId is null)
            return Unauthorized(new { error = "Not authenticated" });

        var result = await _service.RequestPasswordChangeCodeAsync(userId.Value);
        return result.IsSuccess
            ? Ok()
            : BadRequest(new { error = result.Error });
    }

    // ── POST /api/auth/change-password  { currentPassword, newPassword, code } ─
    // Authenticated at the Gateway; we trust the X-User-Id header it sets.
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = CallerId();
        if (userId is null)
            return Unauthorized(new { error = "Not authenticated" });

        var result = await _service.ChangePasswordAsync(userId.Value, request);
        return result.IsSuccess
            ? Ok()
            : BadRequest(new { error = result.Error });
    }

    // ── POST /api/auth/forgot-password  { email } ───────────────
    // Always 200 (no account enumeration).
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        await _service.ForgotPasswordAsync(request);
        return Ok();
    }

    // ── POST /api/auth/reset-password  { email, code, newPassword } ─
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _service.ResetPasswordAsync(request);
        return result.IsSuccess
            ? Ok()
            : BadRequest(new { error = result.Error });
    }

    private Guid? CallerId()
        => Request.Headers.TryGetValue("X-User-Id", out var v) && Guid.TryParse(v.ToString(), out var id)
            ? id
            : null;
}
