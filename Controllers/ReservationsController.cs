// Smart Solar Microgrid Trading System - reservation state and query endpoints.
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SolarGridX.Api.Models.Enums;
using SolarGridX.Api.DTOs.Requests;
using SolarGridX.Api.DTOs.Responses;
using SolarGridX.Api.Services;

namespace SolarGridX.Api.Controllers;

[ApiController]
[Route("api/reservations")]
[Authorize]
public sealed class ReservationsController(ReservationService reservations) : ControllerBase
{
    // Creates a reservation for the current active Prosumer only.
    [HttpPost]
    public async Task<ActionResult<ReservationResponse>> Create(
        CreateReservationRequest request,
        CancellationToken ct)
    {
        var reservation = await reservations.CreateAsync(CurrentId, request, ct);
        return StatusCode(StatusCodes.Status201Created, reservation);
    }

    // Retrieves one reservation after role and ownership checks.
    [HttpGet("{reservationId}")]
    public async Task<ActionResult<ReservationResponse>> Get(
        string reservationId,
        CancellationToken ct)
    {
        var reservation = await reservations.GetAsync(
            reservationId,
            CurrentId,
            CurrentRole,
            ct);

        return Ok(reservation);
    }

    // Supports role-scoped reservation listing, history, pending/current groups and authorized filters.
    [HttpGet]
    public async Task<ActionResult<List<ReservationResponse>>> List(
        [FromQuery] string? group,
        [FromQuery] ReservationStatus? status,
        [FromQuery] string? stationId,
        [FromQuery] string? nic,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var reservationList = await reservations.ListAsync(
            CurrentId,
            CurrentRole,
            group,
            status,
            stationId,
            nic,
            from,
            to,
            ct);

        return Ok(reservationList);
    }

    // Updates a reservation with ownership and twelve-hour notice validation.
    [HttpPut("{reservationId}")]
    public async Task<ActionResult<ReservationResponse>> Update(
        string reservationId,
        UpdateReservationRequest request,
        CancellationToken ct)
    {
        var reservation = await reservations.UpdateAsync(
            reservationId,
            CurrentId,
            CurrentRole,
            request,
            ct);

        return Ok(reservation);
    }

    // Cancels a reservation safely; Grid Operators may assist operationally.
    [HttpPost("{reservationId}/cancel")]
    public async Task<IActionResult> Cancel(string reservationId, CancellationToken ct)
    {
        await reservations.CancelAsync(reservationId, CurrentId, CurrentRole, ct);
        return NoContent();
    }

    // Approves a pending reservation through an operational Grid Operator or Backoffice workflow.
    [Authorize(Roles = nameof(UserRole.GridOperator) + "," + nameof(UserRole.Backoffice))]
    [HttpPost("{reservationId}/approve")]
    public async Task<ActionResult<ReservationResponse>> Approve(
        string reservationId,
        CancellationToken ct)
    {
        var reservation = await reservations.ApproveAsync(reservationId, ct);
        return Ok(reservation);
    }

    // Rejects a pending reservation and releases its held capacity.
    [Authorize(Roles = nameof(UserRole.GridOperator) + "," + nameof(UserRole.Backoffice))]
    [HttpPost("{reservationId}/reject")]
    public async Task<IActionResult> Reject(string reservationId, CancellationToken ct)
    {
        await reservations.RejectAsync(reservationId, ct);
        return NoContent();
    }

    // Returns a fresh opaque QR token only to the owning Prosumer for the approved reservation.
    [HttpPost("{reservationId}/transaction-token")]
    public async Task<ActionResult<QrTokenResponse>> TransactionToken(
        string reservationId,
        CancellationToken ct)
    {
        var token = await reservations.IssueQrTokenAsync(reservationId, CurrentId, ct);
        return Ok(token);
    }

    // Resolves the authenticated subject claim once for all reservation operations.
    private string CurrentId
    {
        get
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Authenticated subject is missing.");
        }
    }

    // Resolves the optional staff role; a null role identifies an NIC-keyed Prosumer profile.
    private UserRole? CurrentRole
    {
        get
        {
            return Enum.TryParse<UserRole>(User.FindFirstValue(ClaimTypes.Role), out var role)
                ? role
                : null;
        }
    }
}
