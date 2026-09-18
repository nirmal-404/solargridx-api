// Smart Solar Microgrid Trading System - safe Prosumer profile response.
using SolarGridX.Api.Models.Enums;

namespace SolarGridX.Api.DTOs.Responses;

public sealed record ProsumerResponse(
    string Id,
    string Nic,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? Address,
    AccountStatus AccountStatus);
