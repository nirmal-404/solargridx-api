// Smart Solar Microgrid Trading System - Google OAuth 2.0 validation service.
using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using SolarGridX.Api.Common;
using SolarGridX.Api.Configuration;

namespace SolarGridX.Api.Services;

public sealed class OAuthService(
    IOptions<OAuthOptions> options,
    ILogger<OAuthService> logger
) : IOAuthService
{
    public async Task<OAuthUserInfo> ValidateGoogleTokenAsync(string idToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new ApiException(400, "OAuth ID token is required.");
        }

        var oauthOptions = options.Value.Google;

        // Support mock/test tokens for offline, test, or local development environments
        if (oauthOptions.AllowTestTokens && idToken.StartsWith("test_token_", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Validating mock test OAuth 2.0 token: {Token}", idToken);
            var parts = idToken.Split(':');
            var email = parts.Length > 1 ? parts[1].Trim().ToLowerInvariant() : "test.user@solargridx.com";
            var name = parts.Length > 2 ? parts[2].Trim() : "Test User";
            var nameParts = name.Split(' ', 2);
            var firstName = nameParts.Length > 0 ? nameParts[0] : "Test";
            var lastName = nameParts.Length > 1 ? nameParts[1] : "User";
            var subId = parts.Length > 3 ? parts[3].Trim() : $"mock_google_sub_{email.GetHashCode():X8}";

            return new OAuthUserInfo(subId, email, firstName, lastName);
        }

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings();
            if (!string.IsNullOrWhiteSpace(oauthOptions.ClientId))
            {
                settings.Audience = [oauthOptions.ClientId];
            }

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Email))
            {
                throw new ApiException(401, "Invalid OAuth ID token payload: missing email address.");
            }

            var firstName = payload.GivenName ?? payload.Name ?? "OAuth";
            var lastName = payload.FamilyName ?? "User";
            return new OAuthUserInfo(payload.Subject, payload.Email.ToLowerInvariant(), firstName, lastName, payload.Picture);
        }
        catch (InvalidJwtException ex)
        {
            logger.LogWarning(ex, "Failed to validate Google OAuth 2.0 ID token");
            throw new ApiException(401, "Invalid Google OAuth 2.0 token: signature or issuer verification failed.");
        }
        catch (Exception ex) when (ex is not ApiException)
        {
            logger.LogError(ex, "Unexpected error during OAuth 2.0 token validation");
            throw new ApiException(401, "Failed to validate external OAuth 2.0 token.");
        }
    }
}
