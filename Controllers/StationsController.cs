// Smart Solar Microgrid Trading System - station administration and discovery endpoints.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SolarGridX.Api.Models.Enums;
using SolarGridX.Api.DTOs.Requests;
using SolarGridX.Api.DTOs.Responses;
using SolarGridX.Api.Services;

namespace SolarGridX.Api.Controllers;

[ApiController]
[Route("api/stations")]
public sealed class StationsController(StationSlotService stations) : ControllerBase
{
    // Creates a station through Backoffice administration.
    [Authorize(Roles = nameof(UserRole.Backoffice))]
    [HttpPost]
    public async Task<ActionResult<StationResponse>> Create(
        CreateStationRequest request,
        CancellationToken ct)
    {
        var station = await stations.CreateStationAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, station);
    }
    
    // Returns active stations to clients, with Backoffice able to request inactive stations too.
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<List<StationResponse>>> List(
        [FromQuery] bool includeInactive,
        CancellationToken ct)
    {
        var activeOnly = !includeInactive || !User.IsInRole(nameof(UserRole.Backoffice));
        var stationList = await stations.GetStationsAsync(activeOnly, ct);
        return Ok(stationList);
    }

    // Returns one station to authenticated clients.
    [Authorize]
    [HttpGet("{stationId}")]
    public async Task<ActionResult<StationResponse>> Get(string stationId, CancellationToken ct)
    {
        var station = await stations.GetStationAsync(stationId, ct);
        return Ok(station);
    }

    // Updates station details through Backoffice administration.
    [Authorize(Roles = nameof(UserRole.Backoffice))]
    [HttpPut("{stationId}")]
    public async Task<ActionResult<StationResponse>> Update(
        string stationId,
        UpdateStationRequest request,
        CancellationToken ct)
    {
        var station = await stations.UpdateStationAsync(stationId, request, ct);
        return Ok(station);
    }

    // Updates operational schedule through Backoffice administration.
    [Authorize(Roles = nameof(UserRole.Backoffice))]
    [HttpPatch("{stationId}/schedule")]
    public async Task<ActionResult<StationResponse>> Schedule(
        string stationId,
        UpdateScheduleRequest request,
        CancellationToken ct)
    {
        var station = await stations.UpdateScheduleAsync(stationId, request, ct);
        return Ok(station);
    }

    // Applies the active-reservation deactivation restriction.
    [Authorize(Roles = nameof(UserRole.Backoffice))]
    [HttpPost("{stationId}/deactivate")]
    public async Task<IActionResult> Deactivate(string stationId, CancellationToken ct)
    {
        await stations.DeactivateStationAsync(stationId, ct);
        return NoContent();
    }

    // Restores a deactivated station through Backoffice administration.
    [Authorize(Roles = nameof(UserRole.Backoffice))]
    [HttpPost("{stationId}/reactivate")]
    public async Task<IActionResult> Reactivate(string stationId, CancellationToken ct)
    {
        await stations.ReactivateStationAsync(stationId, ct);
        return NoContent();
    }

    // Provides active stations near a client-supplied coordinate.
    [Authorize]
    [HttpGet("nearby")]
    public async Task<ActionResult<List<StationResponse>>> Nearby(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double radiusKm,
        CancellationToken ct)
    {
        var nearbyStations = await stations.NearbyAsync(latitude, longitude, radiusKm, ct);
        return Ok(nearbyStations);
    }
}
