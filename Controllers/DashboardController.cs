// Smart Solar Microgrid Trading System - role-scoped dashboard aggregate endpoint.
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SolarGridX.Api.Models.Enums;
using SolarGridX.Api.DTOs.Responses;
using SolarGridX.Api.Services;

namespace SolarGridX.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController(ReservationService reservations) : ControllerBase
{
    // Returns current, pending, approved-future and completed counts calculated from MongoDB.
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryResponse>> Summary(CancellationToken ct)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!Enum.TryParse<UserRole>(User.FindFirstValue(ClaimTypes.Role), out var role))
            return Forbid();
        var summary = await reservations.SummaryAsync(id, role, ct);

        return Ok(summary);
    }
}
