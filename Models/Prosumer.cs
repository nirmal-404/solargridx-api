// Smart Solar Microgrid Trading System - NIC-keyed Prosumer profile document.
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SolarGridX.Api.Models.Enums;

namespace SolarGridX.Api.Models;

public sealed class Prosumer : AuditedDocument
{
    public string Nic { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }

    [BsonRepresentation(BsonType.String)]
    public AccountStatus AccountStatus { get; set; } = AccountStatus.Active;
}
