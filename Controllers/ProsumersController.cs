// Smart Solar Microgrid Trading System - Prosumer lifecycle endpoints.
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SolarGridX.Api.Models.Enums;
using SolarGridX.Api.DTOs.Requests;
using SolarGridX.Api.DTOs.Responses;
using SolarGridX.Api.Services;

namespace SolarGridX.Api.Controllers;

[ApiController]
[Route("api/prosumers")]
[Authorize]
public sealed class ProsumersController(UserService users) : ControllerBase
{
    // Returns a Prosumer profile after Backoffice or self-ownership authorization.
    [HttpGet("{nic}")]
    public async Task<ActionResult<UserResponse>> Get(string nic, CancellationToken ct)
    {
        var user = await users.FindProsumerByNicAsync(nic, ct);
        var isBackoffice = User.IsInRole(nameof(UserRole.Backoffice));
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!isBackoffice && user.Id != currentUserId)
        {
            return Forbid();
        }

        return Ok(user.ToResponse());
    }

    // Updates a profile only for its owner or Backoffice.
    [HttpPut("{nic}")]
    public async Task<ActionResult<UserResponse>> Update(string nic, UpdateProsumerRequest request, CancellationToken ct)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var isBackoffice = User.IsInRole(nameof(UserRole.Backoffice));
        var user = await users.UpdateProsumerAsync(
            nic,
            currentUserId,
            isBackoffice,
            request,
            ct);

        return Ok(user);
    }

    // Records a self-service deactivation request for later administrative handling.
    [Authorize]
    [HttpPost("{nic}/deactivation-request")]
    public async Task<IActionResult> RequestDeactivation(string nic, CancellationToken ct)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await users.RequestDeactivationAsync(nic, currentUserId, ct);
        return NoContent();
    }

    // Lists Prosumer accounts requiring Backoffice action.
    [Authorize(Roles = nameof(UserRole.Backoffice))]
    [HttpGet("pending")]
    public async Task<ActionResult<List<UserResponse>>> Pending(CancellationToken ct)
    {
        var pendingUsers = await users.GetPendingAsync(ct);
        return Ok(pendingUsers);
    }

    // Reactivates a non-active Prosumer account through Backoffice only.
    [Authorize(Roles = nameof(UserRole.Backoffice))]
    [HttpPost("{nic}/reactivate")]
    public async Task<ActionResult<UserResponse>> Reactivate(string nic, CancellationToken ct)
    {
        var user = await users.ReactivateAsync(nic, ct);
        return Ok(user);
    }
}
