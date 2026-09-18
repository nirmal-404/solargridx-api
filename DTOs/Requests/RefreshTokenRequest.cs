// Smart Solar Microgrid Trading System - refresh token request payload.
using System.ComponentModel.DataAnnotations;

namespace SolarGridX.Api.DTOs.Requests;

public sealed class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
