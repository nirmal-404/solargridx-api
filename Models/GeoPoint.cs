// Smart Solar Microgrid Trading System - GeoJSON point model.
namespace SolarGridX.Api.Models;

public sealed class GeoPoint
{
    public string Type { get; set; } = "Point";
    public double[] Coordinates { get; set; } = [];
}
