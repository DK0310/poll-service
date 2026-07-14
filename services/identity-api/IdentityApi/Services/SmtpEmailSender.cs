using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace IdentityApi.Services;

/// <summary>
/// Gmail SMTP sender (MailKit). Config under Smtp: Host/Port/User/Password (Gmail App Password)/FromEmail/FromName.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string body)
    {
        var host = _config["Smtp:Host"] ?? "smtp.gmail.com";
        var port = int.TryParse(_config["Smtp:Port"], out var p) ? p : 587;
        var user = _config["Smtp:User"]
            ?? throw new InvalidOperationException("Smtp:User is not configured");
        // Gmail shows App Passwords as four space-separated groups; strip whitespace so the
        // value can be pasted exactly as displayed.
        var password = (_config["Smtp:Password"]
            ?? throw new InvalidOperationException("Smtp:Password is not configured"))
            .Replace(" ", "");
        var fromEmail = _config["Smtp:FromEmail"] ?? user;
        var fromName = _config["Smtp:FromName"] ?? "Poll Builder";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(user, password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        _logger.LogInformation("Sent '{Subject}' email to {To}", subject, toEmail);
    }
}
