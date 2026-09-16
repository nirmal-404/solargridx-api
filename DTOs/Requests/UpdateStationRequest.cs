// Smart Solar Microgrid Trading System - station update request.
using System.ComponentModel.DataAnnotations;
namespace SolarGridX.Api.DTOs.Requests;

public sealed class UpdateStationRequest
{
    public string? Name { get; init; }

    public string? Description { get; init; }

    [Range(-90, 90)]
    public double? Latitude { get; init; }

    [Range(-180, 180)]
    public double? Longitude { get; init; }

    [Range(0.01, double.MaxValue)]
    public decimal? CapacityKwh { get; init; }

    [Range(0, int.MaxValue)]
    public int? AvailableBatteryStorageSlots { get; init; }
}
