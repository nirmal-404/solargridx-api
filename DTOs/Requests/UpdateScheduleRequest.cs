// Smart Solar Microgrid Trading System - operational schedule update request.
using System.ComponentModel.DataAnnotations;
using SolarGridX.Api.Models;
namespace SolarGridX.Api.DTOs.Requests;

public sealed class UpdateScheduleRequest
{
    [Required]
    public OperationalSchedule Schedule { get; init; } = new();
}
