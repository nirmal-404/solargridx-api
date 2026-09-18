// Smart Solar Microgrid Trading System - Google OAuth 2.0 exchange request.
using System.ComponentModel.DataAnnotations;

namespace SolarGridX.Api.DTOs.Requests;

public sealed class GoogleOAuthRequest
{
    [Required]
    public string IdToken { get; set; } = string.Empty;

    public string? Nic { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
}
