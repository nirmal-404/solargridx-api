// Smart Solar Microgrid Trading System - OAuth service unit tests.
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SolarGridX.Api.Common;
using SolarGridX.Api.Configuration;
using SolarGridX.Api.Services;

namespace SolarGridX.Api.Tests;

public sealed class OAuthServiceTests
{
    private readonly OAuthService _service;

    public OAuthServiceTests()
    {
        var options = Options.Create(new OAuthOptions
        {
            Google = new GoogleOAuthOptions
            {
                ClientId = "test-client-id.apps.googleusercontent.com",
                ClientSecret = "test-secret",
                AllowTestTokens = true
            }
        });

        _service = new OAuthService(options, NullLogger<OAuthService>.Instance);
    }

    [Fact]
    public async Task ValidateGoogleTokenAsync_WithEmptyToken_ThrowsApiException400()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _service.ValidateGoogleTokenAsync(""));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task ValidateGoogleTokenAsync_WithTestToken_ParsesUserInfoCorrectly()
    {
        var testToken = "test_token_:prosumer.test@solargridx.com:Kamal Silva:google_sub_998877";

        var userInfo = await _service.ValidateGoogleTokenAsync(testToken);

        Assert.NotNull(userInfo);
        Assert.Equal("prosumer.test@solargridx.com", userInfo.Email);
        Assert.Equal("Kamal", userInfo.FirstName);
        Assert.Equal("Silva", userInfo.LastName);
        Assert.Equal("google_sub_998877", userInfo.SubjectId);
    }

    [Fact]
    public async Task ValidateGoogleTokenAsync_WithInvalidTokenAndTestingDisabled_ThrowsApiException401()
    {
        var strictOptions = Options.Create(new OAuthOptions
        {
            Google = new GoogleOAuthOptions
            {
                ClientId = "test-client-id.apps.googleusercontent.com",
                AllowTestTokens = false
            }
        });
        var strictService = new OAuthService(strictOptions, NullLogger<OAuthService>.Instance);

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            strictService.ValidateGoogleTokenAsync("invalid_untrusted_raw_jwt"));

        Assert.Equal(401, ex.StatusCode);
    }
}
