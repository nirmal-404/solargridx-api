// Smart Solar Microgrid Trading System - QR verification and completion endpoints.
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SolarGridX.Api.Models.Enums;
using SolarGridX.Api.DTOs.Requests;
using SolarGridX.Api.DTOs.Responses;
using SolarGridX.Api.Services;

namespace SolarGridX.Api.Controllers;

[ApiController]
[Route("api/transactions")]
[Authorize(Roles = nameof(UserRole.GridOperator))]
public sealed class TransactionsController(ReservationService reservations) : ControllerBase
{
    // Verifies a scanned opaque QR token and returns safe operational transaction details.
    [HttpPost("verify")]
    public async Task<ActionResult<TransactionResponse>> Verify(
        VerifyTransactionRequest request,
        CancellationToken ct)
    {
        var transaction = await reservations.VerifyTransactionAsync(request, ct);
        return Ok(transaction);
    }
    
    // Completes a verified transaction once through a conditional server-side transition.
    [HttpPost("{transactionId}/complete")]
    public async Task<ActionResult<TransactionResponse>> Complete(
        string transactionId,
        CancellationToken ct)
    {
        var operatorId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var transaction = await reservations.CompleteTransactionAsync(
            transactionId,
            operatorId,
            ct);

        return Ok(transaction);
    }
}
