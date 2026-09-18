// Smart Solar Microgrid Trading System - refresh token document.
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SolarGridX.Api.Models;

public sealed class RefreshToken : AuditedDocument
{
    public string Token { get; set; } = string.Empty;
    [BsonRepresentation(BsonType.ObjectId)] public string UserId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }
    public string? CreatedByIp { get; set; }

    [BsonIgnore] public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    [BsonIgnore] public bool IsRevoked => RevokedAt.HasValue;
    [BsonIgnore] public bool IsActive => !IsRevoked && !IsExpired;
}
