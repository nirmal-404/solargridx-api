// Smart Solar Microgrid Trading System - OAuth 2.0 external authentication contract.
namespace SolarGridX.Api.Services;

public sealed record OAuthUserInfo(
    string SubjectId,
    string Email,
    string FirstName,
    string LastName,
    string? PictureUrl = null
);

public interface IOAuthService
{
    Task<OAuthUserInfo> ValidateGoogleTokenAsync(string idToken, CancellationToken ct = default);
}
