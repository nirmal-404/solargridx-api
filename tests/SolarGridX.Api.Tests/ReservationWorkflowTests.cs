// Smart Solar Microgrid Trading System - reservation workflow business rule unit tests.
using MongoDB.Bson;
using SolarGridX.Api.Common;
using SolarGridX.Api.Models;
using SolarGridX.Api.Models.Enums;
using SolarGridX.Api.Services;

namespace SolarGridX.Api.Tests;

public sealed class ReservationWorkflowTests
{
    [Fact]
    public void SolarStation_SerializesLocationProperly()
    {
        var station = new SolarStation
        {
            StationId = "ST-01",
            Name = "Test",
            Location = new GeoPoint { Coordinates = [79.84, 6.93] }
        };
        var bson = station.ToBsonDocument();
        var loc = bson["Location"].AsBsonDocument;
        Assert.True(loc.Contains("type"), "Location should contain 'type'");
        Assert.True(loc.Contains("coordinates"), "Location should contain 'coordinates'");
        Assert.Equal("Point", loc["type"].AsString);
    }

    [Fact]
    public void User_OmitsNic_WhenNull()
    {
        var user = new User
        {
            Email = "op@test.local",
            Role = UserRole.GridOperator
        };
        var bson = user.ToBsonDocument();
        Assert.False(bson.Contains("Nic"), "Nic should be omitted when null so sparse unique index works in MongoDB");
    }

    // Confirms that a reservation scheduled in the past is rejected with a 422 error.
    [Fact]
    public void ValidateSevenDayWindow_WithPastTime_Throws422()
    {
        var pastTime = DateTime.UtcNow.AddHours(-1);
        var ex = Assert.Throws<ApiException>(() => ReservationService.ValidateSevenDayWindow(pastTime));
        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("future", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Confirms that a reservation scheduled beyond 7 days in the future is rejected with a 422 error.
    [Fact]
    public void ValidateSevenDayWindow_WithMoreThanSevenDays_Throws422()
    {
        var futureTime = DateTime.UtcNow.AddDays(7).AddHours(2);
        var ex = Assert.Throws<ApiException>(() => ReservationService.ValidateSevenDayWindow(futureTime));
        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("7 days", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Confirms that a reservation scheduled within the 7-day window succeeds.
    [Fact]
    public void ValidateSevenDayWindow_WithValidTimeWithinSevenDays_Passes()
    {
        var validTime = DateTime.UtcNow.AddDays(2);
        ReservationService.ValidateSevenDayWindow(validTime);
    }

    // Confirms that updating a reservation with less than 12 hours notice is rejected with a 409 error.
    [Fact]
    public void EnsureModifiable_WithLessThan12HoursNotice_Throws409()
    {
        var reservation = new EnergyReservation
        {
            ScheduledStartTime = DateTime.UtcNow.AddHours(6),
            Status = ReservationStatus.Pending,
        };
        var ex = Assert.Throws<ApiException>(() => ReservationService.EnsureModifiable(reservation));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("12 hours", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Confirms that updating a reservation with 12 or more hours notice succeeds.
    [Fact]
    public void EnsureModifiable_WithAtLeast12HoursNotice_Passes()
    {
        var reservation = new EnergyReservation
        {
            ScheduledStartTime = DateTime.UtcNow.AddHours(24),
            Status = ReservationStatus.Pending,
        };
        ReservationService.EnsureModifiable(reservation);
    }

    // Confirms that updating an already completed, cancelled, rejected, or expired reservation is blocked.
    [Theory]
    [InlineData(ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.Rejected)]
    [InlineData(ReservationStatus.Completed)]
    [InlineData(ReservationStatus.Expired)]
    public void EnsureModifiable_WithTerminalStatus_Throws409(ReservationStatus status)
    {
        var reservation = new EnergyReservation
        {
            ScheduledStartTime = DateTime.UtcNow.AddHours(48),
            Status = status,
        };
        var ex = Assert.Throws<ApiException>(() => ReservationService.EnsureModifiable(reservation));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("current state", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Confirms that cancelling a reservation with less than 12 hours notice is rejected with a 409 error.
    [Fact]
    public void EnsureCancellable_WithLessThan12HoursNotice_Throws409()
    {
        var reservation = new EnergyReservation
        {
            ScheduledStartTime = DateTime.UtcNow.AddHours(4),
            Status = ReservationStatus.Approved,
        };
        var ex = Assert.Throws<ApiException>(() => ReservationService.EnsureCancellable(reservation));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("12 hours", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Confirms that cancelling a reservation with at least 12 hours notice succeeds.
    [Fact]
    public void EnsureCancellable_WithAtLeast12HoursNotice_Passes()
    {
        var reservation = new EnergyReservation
        {
            ScheduledStartTime = DateTime.UtcNow.AddHours(36),
            Status = ReservationStatus.Approved,
        };
        ReservationService.EnsureCancellable(reservation);
    }

    // Confirms that cancelling a reservation in a terminal status is blocked.
    [Theory]
    [InlineData(ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.Rejected)]
    [InlineData(ReservationStatus.Completed)]
    [InlineData(ReservationStatus.Expired)]
    public void EnsureCancellable_WithTerminalStatus_Throws409(ReservationStatus status)
    {
        var reservation = new EnergyReservation
        {
            ScheduledStartTime = DateTime.UtcNow.AddHours(48),
            Status = status,
        };
        var ex = Assert.Throws<ApiException>(() => ReservationService.EnsureCancellable(reservation));
        Assert.Equal(409, ex.StatusCode);
    }

    // Confirms that booking a slot on a different station than requested throws 422.
    [Fact]
    public void ValidateBookingTarget_WithMismatchedStation_Throws422()
    {
        var station = new SolarStation { StationId = "STATION-A", Status = StationStatus.Active };
        var slot = new EnergyBookingSlot { StationId = "STATION-B", Status = SlotStatus.Active };
        var ex = Assert.Throws<ApiException>(() => ReservationService.ValidateBookingTarget(station, slot, 5.0m));
        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("belong", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Confirms that booking an inactive slot throws 409.
    [Fact]
    public void ValidateBookingTarget_WithInactiveSlot_Throws409()
    {
        var station = new SolarStation { StationId = "STATION-A", Status = StationStatus.Active };
        var slot = new EnergyBookingSlot { StationId = "STATION-A", Status = SlotStatus.Deactivated };
        var ex = Assert.Throws<ApiException>(() => ReservationService.ValidateBookingTarget(station, slot, 5.0m));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("not active", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Confirms that non-positive requested capacity throws 422.
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ValidateBookingTarget_WithZeroOrNegativeCapacity_Throws422(decimal requestedCapacity)
    {
        var station = new SolarStation { StationId = "STATION-A", Status = StationStatus.Active };
        var slot = new EnergyBookingSlot { StationId = "STATION-A", Status = SlotStatus.Active };
        var ex = Assert.Throws<ApiException>(() => ReservationService.ValidateBookingTarget(station, slot, requestedCapacity));
        Assert.Equal(422, ex.StatusCode);
        Assert.Contains("positive", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Confirms that SHA-256 token hashing produces deterministic uppercase hex strings.
    [Fact]
    public void HashToken_ProducesDeterministicUpperHex()
    {
        var rawToken = "test-opaque-qr-token-value";
        var hash1 = ReservationService.HashToken(rawToken);
        var hash2 = ReservationService.HashToken(rawToken);

        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length);
        Assert.Equal(hash1.ToUpperInvariant(), hash1);
    }

    // Confirms that MappingExtensions correctly maps all reservation attributes including ProsumerNic.
    [Fact]
    public void ToResponse_MapsAllReservationFieldsCorrectly()
    {
        var reservation = new EnergyReservation
        {
            Id = "mongo-id-123",
            ReservationId = "RSV-ABC123XYZ",
            StationId = "STATION-A",
            SlotId = "SLT-XYZ987",
            ScheduledStartTime = DateTime.UtcNow.AddHours(20),
            ScheduledEndTime = DateTime.UtcNow.AddHours(22),
            RequestedCapacity = 10.5m,
            Status = ReservationStatus.Pending,
            ProsumerNic = "200012345678",
            Transaction = new TransactionInfo
            {
                TransactionId = "TXN-999888",
                ExpiresAt = DateTime.UtcNow.AddHours(23),
            },
        };

        var response = reservation.ToResponse();

        Assert.Equal(reservation.Id, response.Id);
        Assert.Equal(reservation.ReservationId, response.ReservationId);
        Assert.Equal(reservation.StationId, response.StationId);
        Assert.Equal(reservation.SlotId, response.SlotId);
        Assert.Equal(reservation.ScheduledStartTime, response.ScheduledStartTime);
        Assert.Equal(reservation.ScheduledEndTime, response.ScheduledEndTime);
        Assert.Equal(reservation.RequestedCapacity, response.RequestedCapacity);
        Assert.Equal(reservation.Status, response.Status);
        Assert.Equal("TXN-999888", response.TransactionId);
        Assert.Equal("200012345678", response.ProsumerNic);
    }
}
