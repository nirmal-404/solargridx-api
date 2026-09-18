// Smart Solar Microgrid Trading System - password hashing and JWT issuance services.
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using SolarGridX.Api.Configuration;
using SolarGridX.Api.Models;

namespace SolarGridX.Api.Services;

public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string password, string encodedHash);
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) Create(User user);
    Task<RefreshToken> IssueRefreshTokenAsync(string userId, string? ipAddress = null, CancellationToken ct = default);
    Task<(string AccessToken, DateTime AccessExpiresAt, RefreshToken NewRefreshToken, User User)> RotateRefreshTokenAsync(string token, string? ipAddress = null, CancellationToken ct = default);
    Task RevokeRefreshTokenAsync(string token, string? ipAddress = null, CancellationToken ct = default);
    Task RevokeAllUserTokensAsync(string userId, CancellationToken ct = default);
}

public sealed class PasswordService : IPasswordService
{
    // Hashes a password with PBKDF2 and a random salt; no password is persisted as plaintext.
    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210_000, HashAlgorithmName.SHA512, 32);
        return $"v1.210000.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    // Verifies a supplied password without leaking comparison timing information.
    public bool Verify(string password, string encodedHash)
    {
        var parts = encodedHash.Split('.');
        if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations))
            return false;
        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA512,
            expected.Length
        );
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

public sealed class TokenService(
    IOptions<JwtOptions> options,
    Infrastructure.MongoContext db,
    ILogger<TokenService> logger
) : ITokenService
{
    // Issues a short-lived JWT containing identity, optional staff role and NIC-based Prosumer identity.
    public (string Token, DateTime ExpiresAt) Create(User user)
    {
        var value = options.Value;
        if (string.IsNullOrWhiteSpace(value.Secret) || value.Secret.Length < 32)
            throw new InvalidOperationException(
                "Jwt:Secret must be configured with at least 32 characters."
            );
        var expires = DateTime.UtcNow.AddMinutes(value.ExpirationMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
        };
        if (user.Role.HasValue)
            claims.Add(new Claim(ClaimTypes.Role, user.Role.Value.ToString()));
        if (user.IsProsumer)
            claims.Add(new Claim("account_kind", "prosumer"));
        if (!string.IsNullOrWhiteSpace(user.Nic))
            claims.Add(new Claim("nic", user.Nic));
        var token = new JwtSecurityToken(
            value.Issuer,
            value.Audience,
            claims,
            expires: expires,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(value.Secret)),
                SecurityAlgorithms.HmacSha256
            )
        );
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    // Generates, stores, and returns a cryptographically secure refresh token for an active user session.
    public async Task<RefreshToken> IssueRefreshTokenAsync(string userId, string? ipAddress = null, CancellationToken ct = default)
    {
        var days = options.Value.RefreshTokenExpirationDays > 0 ? options.Value.RefreshTokenExpirationDays : 7;
        var refreshToken = new RefreshToken
        {
            Token = GenerateSecureToken(),
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(days),
            CreatedByIp = ipAddress,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await db.RefreshTokens.InsertOneAsync(refreshToken, cancellationToken: ct);
        return refreshToken;
    }

    // Rotates a valid refresh token and issues a new access token while guarding against token reuse attacks.
    public async Task<(string AccessToken, DateTime AccessExpiresAt, RefreshToken NewRefreshToken, User User)> RotateRefreshTokenAsync(
        string token,
        string? ipAddress = null,
        CancellationToken ct = default
    )
    {
        var existing = await db.RefreshTokens.Find(x => x.Token == token).FirstOrDefaultAsync(ct);
        if (existing is null)
        {
            throw new Common.ApiException(401, "Refresh token does not exist.");
        }

        if (existing.IsRevoked)
        {
            logger.LogWarning("Refresh token reuse attempted for user {UserId}. Revoking all user tokens.", existing.UserId);
            await RevokeAllUserTokensAsync(existing.UserId, ct);
            throw new Common.ApiException(401, "Invalid refresh token: token has been revoked or reused.");
        }

        if (existing.IsExpired)
        {
            throw new Common.ApiException(401, "Refresh token has expired. Please sign in again.");
        }

        var user = await db.Users.Find(x => x.Id == existing.UserId).FirstOrDefaultAsync(ct);
        if (user is null || user.AccountStatus != Models.Enums.AccountStatus.Active)
        {
            throw new Common.ApiException(403, "User account is inactive or not found.");
        }

        var newRefreshToken = new RefreshToken
        {
            Token = GenerateSecureToken(),
            UserId = existing.UserId,
            ExpiresAt = DateTime.UtcNow.AddDays(options.Value.RefreshTokenExpirationDays > 0 ? options.Value.RefreshTokenExpirationDays : 7),
            CreatedByIp = ipAddress,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        existing.RevokedAt = DateTime.UtcNow;
        existing.ReplacedByToken = newRefreshToken.Token;
        existing.UpdatedAt = DateTime.UtcNow;

        await db.RefreshTokens.ReplaceOneAsync(x => x.Id == existing.Id, existing, cancellationToken: ct);
        await db.RefreshTokens.InsertOneAsync(newRefreshToken, cancellationToken: ct);

        var (accessToken, accessExpiresAt) = Create(user);
        return (accessToken, accessExpiresAt, newRefreshToken, user);
    }

    // Explicitly revokes a single refresh token (used on logout).
    public async Task RevokeRefreshTokenAsync(string token, string? ipAddress = null, CancellationToken ct = default)
    {
        var existing = await db.RefreshTokens.Find(x => x.Token == token).FirstOrDefaultAsync(ct);
        if (existing is null || existing.IsRevoked)
            return;

        existing.RevokedAt = DateTime.UtcNow;
        existing.UpdatedAt = DateTime.UtcNow;
        await db.RefreshTokens.ReplaceOneAsync(x => x.Id == existing.Id, existing, cancellationToken: ct);
    }

    // Revokes all active refresh tokens for a user (e.g. after compromise, password reset, or account lock).
    public async Task RevokeAllUserTokensAsync(string userId, CancellationToken ct = default)
    {
        var update = MongoDB.Driver.Builders<RefreshToken>.Update
            .Set(x => x.RevokedAt, DateTime.UtcNow)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        await db.RefreshTokens.UpdateManyAsync(
            x => x.UserId == userId && x.RevokedAt == null,
            update,
            cancellationToken: ct
        );
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
