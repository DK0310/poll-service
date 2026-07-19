using Google.Apis.Auth;

namespace IdentityApi.Services;

/// <summary>
/// Checks a Google ID token against Google's public keys and confirms its audience is our own
/// Google:ClientId (so a token minted for a different app is rejected). Returns null if invalid.
/// </summary>
public class GoogleTokenVerifier : IGoogleTokenVerifier
{
    private readonly IConfiguration _config;
    private readonly ILogger<GoogleTokenVerifier> _logger;

    public GoogleTokenVerifier(IConfiguration config, ILogger<GoogleTokenVerifier> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<GoogleUser?> VerifyAsync(string idToken)
    {
        var clientId = _config["Google:ClientId"]
            ?? throw new InvalidOperationException("Google:ClientId is not configured");

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [clientId]
            };
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            return new GoogleUser(payload.Subject, payload.Email, payload.EmailVerified);
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "Rejected an invalid Google ID token");
            return null;
        }
    }
}
