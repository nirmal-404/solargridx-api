// Smart Solar Microgrid Trading System - microgrid operating-hours models.
namespace SolarGridX.Api.Models;

// Holds the weekly opening-hours schedule for a solar station.
public sealed class OperationalSchedule
{
    // IANA time zone identifier for the station, used when displaying local times to clients.
    public string? TimeZoneId { get; set; }

    // Each entry describes the open and close time for one day of the week.
    public List<DailyHours> Days { get; set; } = [];
}

// Describes the operating window for a single day of the week.
public sealed class DailyHours
{
    // The day this entry applies to, e.g. Monday.
    public DayOfWeek Day { get; set; }

    // Station opens at this local time. Must be strictly before Close.
    public TimeOnly Open { get; set; }

    // Station closes at this local time. Must be strictly after Open.
    public TimeOnly Close { get; set; }
}
