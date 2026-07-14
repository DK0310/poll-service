namespace IdentityApi.Services;

/// <summary>
/// Sends transactional email. Abstracted so tests can capture messages without a real SMTP server.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string body);
}
