// Smart Solar Microgrid Trading System - station creation request.
using System.ComponentModel.DataAnnotations;
using SolarGridX.Api.Models;
namespace SolarGridX.Api.DTOs.Requests;

public sealed class CreateStationRequest
{
    [Required]
    [StringLength(50)]
    public string StationId { get; init; } = string.Empty;

    [Required]
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    [Range(-90, 90)]
    public double Latitude { get; init; }

    [Range(-180, 180)]
    public double Longitude { get; init; }

    [Range(0.01, double.MaxValue)]
    public decimal CapacityKwh { get; init; }

    [Range(0, int.MaxValue)]
    public int AvailableBatteryStorageSlots { get; init; }

    public OperationalSchedule? Schedule { get; init; }
}
