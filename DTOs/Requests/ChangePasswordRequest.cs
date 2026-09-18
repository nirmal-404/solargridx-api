// Smart Solar Microgrid Trading System - change password request payload.
using System.ComponentModel.DataAnnotations;

namespace SolarGridX.Api.DTOs.Requests;

public sealed class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(8, ErrorMessage = "New password must have at least 8 characters.")]
    public string NewPassword { get; set; } = string.Empty;
}
