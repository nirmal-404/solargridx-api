// Smart Solar Microgrid Trading System - standard OAuth 2.0 token request.
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace SolarGridX.Api.DTOs.Requests;

public sealed class OAuthTokenRequest
{
    [Required]
    [JsonPropertyName("grant_type")]
    [FromForm(Name = "grant_type")]
    public string GrantType { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    [FromForm(Name = "username")]
    public string? Username { get; set; }

    [JsonPropertyName("password")]
    [FromForm(Name = "password")]
    public string? Password { get; set; }

    [JsonPropertyName("refresh_token")]
    [FromForm(Name = "refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("token")]
    [FromForm(Name = "token")]
    public string? Token { get; set; }

    [JsonPropertyName("client_id")]
    [FromForm(Name = "client_id")]
    public string? ClientId { get; set; }

    [JsonPropertyName("client_secret")]
    [FromForm(Name = "client_secret")]
    public string? ClientSecret { get; set; }

    [JsonPropertyName("scope")]
    [FromForm(Name = "scope")]
    public string? Scope { get; set; }
}
