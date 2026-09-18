// Smart Solar Microgrid Trading System - Token and RefreshToken tests.
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using SolarGridX.Api.Configuration;
using SolarGridX.Api.Models;
using SolarGridX.Api.Models.Enums;
using SolarGridX.Api.Services;

namespace SolarGridX.Api.Tests;

public sealed class TokenServiceTests
{
    private readonly JwtOptions _jwtOptions = new()
    {
        Secret = "super-secret-key-that-is-at-least-32-characters-long",
        Issuer = "SolarGridX.Api.Tests",
        Audience = "SolarGridX.Tests",
        ExpirationMinutes = 30,
        RefreshTokenExpirationDays = 7
    };

    [Fact]
    public void Create_GeneratesValidJwtWithClaims()
    {
        var service = new TokenService(
            Options.Create(_jwtOptions),
            null!, // db not required for purely synchronous JWT creation
            null!
        );

        var user = new User
        {
            Id = "60f1b2c3d4e5f6a7b8c9d0e1",
            Email = "prosumer@solargridx.com",
            Nic = "199012345678",
            IsProsumer = true,
            Role = null,
            AccountStatus = AccountStatus.Active
        };

        var (token, expiresAt) = service.Create(user);

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.True(expiresAt > DateTime.UtcNow);

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        Assert.Equal(_jwtOptions.Issuer, jwtToken.Issuer);
        Assert.Contains(jwtToken.Claims, c => c.Type == ClaimTypes.NameIdentifier && c.Value == user.Id);
        Assert.Contains(jwtToken.Claims, c => c.Type == "account_kind" && c.Value == "prosumer");
        Assert.Contains(jwtToken.Claims, c => c.Type == "nic" && c.Value == user.Nic);
    }

    [Fact]
    public void RefreshToken_ModelLifecycleProperties_WorkAsExpected()
    {
        var activeToken = new RefreshToken
        {
            Token = "secure_token_123",
            UserId = "user_1",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = null
        };

        Assert.False(activeToken.IsExpired);
        Assert.False(activeToken.IsRevoked);
        Assert.True(activeToken.IsActive);

        var revokedToken = new RefreshToken
        {
            Token = "secure_token_456",
            UserId = "user_2",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = DateTime.UtcNow
        };

        Assert.True(revokedToken.IsRevoked);
        Assert.False(revokedToken.IsActive);

        var expiredToken = new RefreshToken
        {
            Token = "secure_token_789",
            UserId = "user_3",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            RevokedAt = null
        };

        Assert.True(expiredToken.IsExpired);
        Assert.False(expiredToken.IsActive);
    }
}
