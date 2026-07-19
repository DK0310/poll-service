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

    // Creates an unverified account and emails an OTP. No token is returned until the email is verified.
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _service.RegisterAsync(request);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        var result = await _service.VerifyEmailAsync(request);
        return result.IsSuccess
            ? Ok(new AuthResponse { Token = result.Value! })
            : BadRequest(new { error = result.Error });
    }

    [HttpPost("resend-code")]
    public async Task<IActionResult> ResendCode([FromBody] ResendCodeRequest request)
    {
        var result = await _service.ResendCodeAsync(request);
        return result.IsSuccess
            ? Ok()
            : BadRequest(new { error = result.Error });
    }

    // NOTE: bad credentials return 400, not 401, on purpose. The frontend has a global 401 handler
    // that force-logs-out; a failed login must not trip it.
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _service.LoginAsync(request);
        return result.IsSuccess
            ? Ok(new AuthResponse { Token = result.Value! })
            : BadRequest(new { error = result.Error });
    }

    [HttpPost("google")]
    public async Task<IActionResult> Google([FromBody] GoogleLoginRequest request)
    {
        var result = await _service.GoogleAsync(request);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    // Gateway enforces auth on this route; we read the caller from the X-User-Id header it sets.
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

    // Emails a password-change OTP to the caller's own address (auth enforced at the gateway).
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

    // Always returns 200, even for an unknown email, so it can't be used to probe which emails exist.
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        await _service.ForgotPasswordAsync(request);
        return Ok();
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _service.ResetPasswordAsync(request);
        return result.IsSuccess
            ? Ok()
            : BadRequest(new { error = result.Error });
    }

    // The gateway put this here after validating the JWT; identity-api never re-parses the token.
    private Guid? CallerId()
        => Request.Headers.TryGetValue("X-User-Id", out var v) && Guid.TryParse(v.ToString(), out var id)
            ? id
            : null;
}
