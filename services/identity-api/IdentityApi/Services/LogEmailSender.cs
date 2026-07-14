namespace IdentityApi.Services;

/// <summary>
/// Development fallback for <see cref="IEmailSender"/> used when Gmail SMTP isn't configured.
/// It logs the message (including the OTP) instead of sending it, so the auth flows are
/// testable locally without real credentials. Never selected once Smtp:User/Password are set.
/// </summary>
public class LogEmailSender : IEmailSender
{
    private readonly ILogger<LogEmailSender> _logger;

    public LogEmailSender(ILogger<LogEmailSender> logger) => _logger = logger;

    public Task SendAsync(string toEmail, string subject, string body)
    {
        _logger.LogWarning(
            "SMTP not configured — email NOT sent. To: {To} | Subject: {Subject}\n{Body}",
            toEmail, subject, body);
        return Task.CompletedTask;
    }
}
