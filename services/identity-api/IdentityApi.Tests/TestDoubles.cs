using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using IdentityApi.Services;

namespace IdentityApi.Tests;

/// <summary>Captures the last OTP emailed to each address so tests can read it back.</summary>
public class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentDictionary<string, string> _lastCode = new();

    public Task SendAsync(string toEmail, string subject, string body)
    {
        var code = Regex.Match(body, @"\d{6}").Value;
        _lastCode[toEmail] = code;
        return Task.CompletedTask;
    }

    public string CodeFor(string email) =>
        _lastCode.TryGetValue(email.Trim().ToLowerInvariant(), out var c) ? c : "";
}

/// <summary>
/// Maps a fake ID token of the form "valid:{email}" to a verified Google user; anything else is invalid.
/// </summary>
public class FakeGoogleTokenVerifier : IGoogleTokenVerifier
{
    public Task<GoogleUser?> VerifyAsync(string idToken)
    {
        if (idToken.StartsWith("valid:", StringComparison.Ordinal))
        {
            var email = idToken["valid:".Length..].Trim().ToLowerInvariant();
            return Task.FromResult<GoogleUser?>(new GoogleUser($"google-{email}", email, true));
        }
        return Task.FromResult<GoogleUser?>(null);
    }
}
