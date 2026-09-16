// Smart Solar Microgrid Trading System - station and booking-slot business workflows.
using MongoDB.Driver;
using SolarGridX.Api.Common;
using SolarGridX.Api.DTOs.Requests;
using SolarGridX.Api.DTOs.Responses;
using SolarGridX.Api.Infrastructure;
using SolarGridX.Api.Models;
using SolarGridX.Api.Models.Enums;

namespace SolarGridX.Api.Services;

public sealed class StationSlotService(MongoContext db)
{
    // Creates an active station and stores coordinates in GeoJSON longitude-latitude order.
    public async Task<StationResponse> CreateStationAsync(
        CreateStationRequest request,
        CancellationToken ct
    )
    {
        ValidateSchedule(request.Schedule);
        var stationId = request.StationId.Trim().ToUpperInvariant();
        if (await db.Stations.Find(x => x.StationId == stationId).AnyAsync(ct))
            throw new ApiException(409, "Station ID is already in use.");
        var station = new SolarStation
        {
            StationId = stationId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Location = new GeoPoint { Coordinates = [request.Longitude, request.Latitude] },
            CapacityKwh = request.CapacityKwh,
            AvailableBatteryStorageSlots = request.AvailableBatteryStorageSlots,
            Schedule = request.Schedule ?? new OperationalSchedule(),
        };
        await db.Stations.InsertOneAsync(station, cancellationToken: ct);
        return station.ToResponse();
    }

    // Returns stations; Prosumer callers should request active stations only.
    public async Task<List<StationResponse>> GetStationsAsync(bool activeOnly, CancellationToken ct)
    {
        var filter = activeOnly
            ? Builders<SolarStation>.Filter.Eq(x => x.Status, StationStatus.Active)
            : Builders<SolarStation>.Filter.Empty;
        return (await db.Stations.Find(filter).SortBy(x => x.Name).ToListAsync(ct))
            .Select(x => x.ToResponse())
            .ToList();
    }

    // Retrieves a station by business identifier.
    public async Task<StationResponse> GetStationAsync(string stationId, CancellationToken ct) =>
        (await FindStationAsync(stationId, ct)).ToResponse();

    // Updates mutable station details and preserves GeoJSON ordering when coordinates are changed.
    public async Task<StationResponse> UpdateStationAsync(
        string stationId,
        UpdateStationRequest request,
        CancellationToken ct
    )
    {
        var station = await FindStationAsync(stationId, ct);
        if (request.Latitude.HasValue != request.Longitude.HasValue)
            throw new ApiException(400, "Latitude and longitude must be supplied together.");
        station.Name = request.Name?.Trim() ?? station.Name;
        station.Description = request.Description?.Trim() ?? station.Description;
        station.CapacityKwh = request.CapacityKwh ?? station.CapacityKwh;
        station.AvailableBatteryStorageSlots =
            request.AvailableBatteryStorageSlots ?? station.AvailableBatteryStorageSlots;
        if (request.Latitude.HasValue)
            station.Location = new GeoPoint
            {
                Coordinates = [request.Longitude!.Value, request.Latitude.Value],
            };
        station.UpdatedAt = DateTime.UtcNow;
        await db.Stations.ReplaceOneAsync(x => x.Id == station.Id, station, cancellationToken: ct);
        return station.ToResponse();
    }

    // Replaces a station schedule after validating each open-close interval.
    public async Task<StationResponse> UpdateScheduleAsync(
        string stationId,
        UpdateScheduleRequest request,
        CancellationToken ct
    )
    {
        ValidateSchedule(request.Schedule);
        var station = await FindStationAsync(stationId, ct);
        station.Schedule = request.Schedule;
        station.UpdatedAt = DateTime.UtcNow;
        await db.Stations.ReplaceOneAsync(x => x.Id == station.Id, station, cancellationToken: ct);
        return station.ToResponse();
    }

    // Prevents deactivation where any Pending or Approved reservation still requires station operation.
    public async Task DeactivateStationAsync(string stationId, CancellationToken ct)
    {
        var station = await FindStationAsync(stationId, ct);
        var active = await db
            .Reservations.Find(x =>
                x.StationId == station.StationId
                && (x.Status == ReservationStatus.Pending || x.Status == ReservationStatus.Approved)
            )
            .AnyAsync(ct);
        if (active)
            throw new ApiException(
                409,
                "Station cannot be deactivated while it has pending or approved reservations."
            );
        await db.Stations.UpdateOneAsync(
            x => x.Id == station.Id,
            Builders<SolarStation>
                .Update.Set(x => x.Status, StationStatus.Deactivated)
                .Set(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken: ct
        );
    }

    // Restores a previously deactivated station for future operations.
    public async Task ReactivateStationAsync(string stationId, CancellationToken ct)
    {
        var station = await FindStationAsync(stationId, ct);
        await db.Stations.UpdateOneAsync(
            x => x.Id == station.Id,
            Builders<SolarStation>
                .Update.Set(x => x.Status, StationStatus.Active)
                .Set(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken: ct
        );
    }

    // Uses MongoDB's 2dsphere index to find active nearby stations with a bounded distance.
    public async Task<List<StationResponse>> NearbyAsync(
        double latitude,
        double longitude,
        double radiusKm,
        CancellationToken ct
    )
    {
        if (radiusKm is <= 0 or > 100)
            throw new ApiException(400, "radiusKm must be greater than zero and no more than 100.");
        var point =
            new MongoDB.Driver.GeoJsonObjectModel.GeoJsonPoint<MongoDB.Driver.GeoJsonObjectModel.GeoJson2DGeographicCoordinates>(
                new(longitude, latitude)
            );
        var filter =
            Builders<SolarStation>.Filter.NearSphere(x => x.Location, point, radiusKm * 1000)
            & Builders<SolarStation>.Filter.Eq(x => x.Status, StationStatus.Active);
        return (await db.Stations.Find(filter).ToListAsync(ct))
            .Select(x => x.ToResponse())
            .ToList();
    }

    // Creates a slot only for an active station and initializes its available capacity to full capacity.
    public async Task<SlotResponse> CreateSlotAsync(CreateSlotRequest request, CancellationToken ct)
    {
        var station = await FindStationAsync(request.StationId, ct);
        if (station.Status != StationStatus.Active)
            throw new ApiException(409, "Slots cannot be created for a deactivated station.");
        ValidateSlotTimes(request.StartTime, request.EndTime);
        var slot = new EnergyBookingSlot
        {
            SlotId = $"SLT-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            StationId = station.StationId,
            StartTime = EnsureUtc(request.StartTime),
            EndTime = EnsureUtc(request.EndTime),
            Capacity = request.Capacity,
            AvailableCapacity = request.Capacity,
        };
        await db.Slots.InsertOneAsync(slot, cancellationToken: ct);
        return slot.ToResponse();
    }

    // Returns slots for administration or active future slots for Prosumer booking.
    public async Task<List<SlotResponse>> GetSlotsAsync(
        string? stationId,
        bool availableOnly,
        CancellationToken ct
    )
    {
        var filter = Builders<EnergyBookingSlot>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(stationId))
            filter &= Builders<EnergyBookingSlot>.Filter.Eq(
                x => x.StationId,
                stationId.Trim().ToUpperInvariant()
            );
        if (availableOnly)
            filter &=
                Builders<EnergyBookingSlot>.Filter.Eq(x => x.Status, SlotStatus.Active)
                & Builders<EnergyBookingSlot>.Filter.Gt(x => x.AvailableCapacity, 0)
                & Builders<EnergyBookingSlot>.Filter.Gt(x => x.StartTime, DateTime.UtcNow);
        return (await db.Slots.Find(filter).SortBy(x => x.StartTime).ToListAsync(ct))
            .Select(x => x.ToResponse())
            .ToList();
    }

    // Retrieves a single slot by its generated business identifier.
    public async Task<SlotResponse> GetSlotAsync(string slotId, CancellationToken ct) =>
        (await FindSlotAsync(slotId, ct)).ToResponse();

    // Updates slot times/capacity while preventing capacity below already committed reservations.
    public async Task<SlotResponse> UpdateSlotAsync(
        string slotId,
        UpdateSlotRequest request,
        CancellationToken ct
    )
    {
        var slot = await FindSlotAsync(slotId, ct);
        var start = request.StartTime ?? slot.StartTime;
        var end = request.EndTime ?? slot.EndTime;
        ValidateSlotTimes(start, end);
        var capacity = request.Capacity ?? slot.Capacity;
        var reserved = slot.Capacity - slot.AvailableCapacity;
        if (capacity < reserved)
            throw new ApiException(
                409,
                "Capacity cannot be reduced below already reserved capacity."
            );
        slot.StartTime = EnsureUtc(start);
        slot.EndTime = EnsureUtc(end);
        slot.Capacity = capacity;
        slot.AvailableCapacity = request.AvailableCapacity ?? capacity - reserved;
        if (slot.AvailableCapacity < 0 || slot.AvailableCapacity > slot.Capacity - reserved)
            throw new ApiException(
                422,
                "Available capacity conflicts with committed reservations."
            );
        slot.UpdatedAt = DateTime.UtcNow;
        await db.Slots.ReplaceOneAsync(x => x.Id == slot.Id, slot, cancellationToken: ct);
        return slot.ToResponse();
    }

    // Deactivates a slot only where it has no capacity held by active reservations.
    public async Task DeactivateSlotAsync(string slotId, CancellationToken ct)
    {
        var slot = await FindSlotAsync(slotId, ct);
        if (slot.AvailableCapacity != slot.Capacity)
            throw new ApiException(409, "Slot cannot be deactivated while capacity is reserved.");
        await db.Slots.UpdateOneAsync(
            x => x.Id == slot.Id,
            Builders<EnergyBookingSlot>
                .Update.Set(x => x.Status, SlotStatus.Deactivated)
                .Set(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken: ct
        );
    }

    // Finds a station or standardizes the not-found response.
    public async Task<SolarStation> FindStationAsync(string stationId, CancellationToken ct) =>
        await db
            .Stations.Find(x => x.StationId == stationId.Trim().ToUpperInvariant())
            .FirstOrDefaultAsync(ct)
        ?? throw new ApiException(404, "Station was not found.");

    // Finds a slot or standardizes the not-found response.
    public async Task<EnergyBookingSlot> FindSlotAsync(string slotId, CancellationToken ct) =>
        await db
            .Slots.Find(x => x.SlotId == slotId.Trim().ToUpperInvariant())
            .FirstOrDefaultAsync(ct)
        ?? throw new ApiException(404, "Slot was not found.");

    // Validates temporal bounds for an energy availability interval.
    private static void ValidateSlotTimes(DateTime start, DateTime end)
    {
        if (EnsureUtc(start) >= EnsureUtc(end))
            throw new ApiException(422, "Slot end time must be after start time.");
    }

    // Prevents malformed daily schedules from entering persisted station documents.
    private static void ValidateSchedule(OperationalSchedule? schedule)
    {
        if (schedule is null)
            return;
        if (
            schedule.Days.GroupBy(x => x.Day).Any(x => x.Count() > 1)
            || schedule.Days.Any(x => x.Open >= x.Close)
        )
            throw new ApiException(422, "Schedule requires one valid open-close interval per day.");
    }

    // Interprets unspecified input as UTC at the HTTP boundary and stores UTC in MongoDB.
    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
