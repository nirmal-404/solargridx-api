// Smart Solar Microgrid Trading System - authentication endpoints.
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SolarGridX.Api.DTOs.Requests;
using SolarGridX.Api.DTOs.Responses;
using SolarGridX.Api.Services;

namespace SolarGridX.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(UserService users) : ControllerBase
{
    // Registers a new Pending Prosumer account using its NIC business identifier.
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var user = await users.RegisterAsync(request, ct);
        return CreatedAtAction(nameof(Me), new { }, user);
    }

    // Authenticates an active account and issues an access token.
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        return Ok(await users.LoginAsync(request, ct));
    }

    // Returns the current account without exposing password or token internals.
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await users.GetCurrentAsync(userId, ct);
        return Ok(user);
    }
}
