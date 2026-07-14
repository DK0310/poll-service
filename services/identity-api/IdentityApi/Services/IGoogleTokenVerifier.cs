namespace IdentityApi.Services;

/// <summary>
/// Verifies a Google ID token (signature + audience) and returns the payload we need.
/// Abstracted so tests don't call Google's servers.
/// </summary>
public interface IGoogleTokenVerifier
{
    Task<GoogleUser?> VerifyAsync(string idToken);
}

public record GoogleUser(string Subject, string Email, bool EmailVerified);
