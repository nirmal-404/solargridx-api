// Smart Solar Microgrid Trading System - authentication and OAuth 2.0 endpoints.
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SolarGridX.Api.Common;
using SolarGridX.Api.Configuration;
using SolarGridX.Api.DTOs.Requests;
using SolarGridX.Api.DTOs.Responses;
using SolarGridX.Api.Services;

namespace SolarGridX.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserService users,
    ITokenService tokens,
    IOAuthService oauth,
    IOptions<JwtOptions> jwtOptions
) : ControllerBase
{
    // Registers a new Pending Prosumer account using its NIC business identifier.
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var user = await users.RegisterAsync(request, ct);
        return CreatedAtAction(nameof(Me), new { }, user);
    }

    // Authenticates an active account and issues an access token and refresh token.
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return Ok(await users.LoginAsync(request, ip, ct));
    }

    // Standard RFC 6749 Open Authorization 2.0 token endpoint (JSON).
    [HttpPost("oauth/token")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(OAuthTokenResponse), StatusCodes.Status200OK)]
    public Task<IActionResult> OAuthTokenJson([FromBody] OAuthTokenRequest request, CancellationToken ct) =>
        HandleOAuthTokenRequestAsync(request, ct);

    // Standard RFC 6749 Open Authorization 2.0 token endpoint (form-urlencoded).
    [HttpPost("oauth/token")]
    [Consumes("application/x-www-form-urlencoded")]
    [ActionName("OAuthTokenForm")]
    [ProducesResponseType(typeof(OAuthTokenResponse), StatusCodes.Status200OK)]
    public Task<IActionResult> OAuthTokenForm([FromForm] OAuthTokenRequest request, CancellationToken ct) =>
        HandleOAuthTokenRequestAsync(request, ct);

    // Internal handler for RFC 6749 grant types.
    private async Task<IActionResult> HandleOAuthTokenRequestAsync(OAuthTokenRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.GrantType))
        {
            return BadRequest(new { error = "invalid_request", error_description = "The grant_type parameter is required." });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var expiresInSeconds = jwtOptions.Value.ExpirationMinutes * 60;

        switch (request.GrantType.ToLowerInvariant())
        {
            case "password":
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(new { error = "invalid_request", error_description = "Username and password are required for password grant." });
                }

                var loginRes = await users.LoginAsync(new LoginRequest { Email = request.Username, Password = request.Password }, ip, ct);
                return Ok(new OAuthTokenResponse(loginRes.AccessToken, "Bearer", expiresInSeconds, loginRes.RefreshToken, "openid profile email"));

            case "refresh_token":
                if (string.IsNullOrWhiteSpace(request.RefreshToken))
                {
                    return BadRequest(new { error = "invalid_request", error_description = "The refresh_token parameter is required." });
                }

                var (newAccessToken, _, newRefreshToken, _) = await tokens.RotateRefreshTokenAsync(request.RefreshToken, ip, ct);
                return Ok(new OAuthTokenResponse(newAccessToken, "Bearer", expiresInSeconds, newRefreshToken.Token, "openid profile email"));

            case "google":
            case "urn:ietf:params:oauth:grant-type:token-exchange":
                var tokenToExchange = request.Token ?? request.RefreshToken;
                if (string.IsNullOrWhiteSpace(tokenToExchange))
                {
                    return BadRequest(new { error = "invalid_request", error_description = "A valid token or id_token is required for token exchange." });
                }

                var oauthUser = await oauth.ValidateGoogleTokenAsync(tokenToExchange, ct);
                var oauthLogin = await users.LoginWithOAuthAsync(oauthUser, ip, null, ct);
                return Ok(new OAuthTokenResponse(oauthLogin.AccessToken, "Bearer", expiresInSeconds, oauthLogin.RefreshToken, "openid profile email"));

            default:
                return BadRequest(new
                {
                    error = "unsupported_grant_type",
                    error_description = $"Grant type '{request.GrantType}' is unsupported. Allowed: password, refresh_token, google, urn:ietf:params:oauth:grant-type:token-exchange."
                });
        }
    }

    // Google OAuth 2.0 ID token authentication endpoint for Web and Mobile clients.
    [HttpPost("oauth/google")]
    public async Task<ActionResult<LoginResponse>> GoogleLogin(GoogleOAuthRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var oauthUser = await oauth.ValidateGoogleTokenAsync(request.IdToken, ct);
        var response = await users.LoginWithOAuthAsync(oauthUser, ip, request, ct);
        return Ok(response);
    }

    // Rotates a valid refresh token and issues a new access token.
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh(RefreshTokenRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (accessToken, accessExpiresAt, newRefreshToken, user) = await tokens.RotateRefreshTokenAsync(request.RefreshToken, ip, ct);
        return Ok(new LoginResponse(accessToken, accessExpiresAt, user.ToResponse(), newRefreshToken.Token, newRefreshToken.ExpiresAt));
    }

    // Revokes the supplied refresh token to safely log out.
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await tokens.RevokeRefreshTokenAsync(request.RefreshToken, ip, ct);
        return Ok(new { message = "Logged out successfully." });
    }

    // Changes the authenticated account password and invalidates previous sessions.
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await users.ChangePasswordAsync(userId, request, ct);
        return Ok(new { message = "Password changed successfully. All previous sessions have been invalidated." });
    }

    // Returns the current account without exposing password or token internals.
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await users.GetCurrentAsync(userId, ct);
        return Ok(user);
    }
}
