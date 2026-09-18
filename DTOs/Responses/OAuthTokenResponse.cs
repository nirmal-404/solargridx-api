// Smart Solar Microgrid Trading System - standard RFC 6749 OAuth 2.0 token response.
using System.Text.Json.Serialization;

namespace SolarGridX.Api.DTOs.Responses;

public sealed record OAuthTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken = null,
    [property: JsonPropertyName("scope")] string? Scope = null);
