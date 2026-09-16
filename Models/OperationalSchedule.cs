// Smart Solar Microgrid Trading System - microgrid operating-hours models.
namespace SolarGridX.Api.Models;

public sealed class OperationalSchedule { 
    public string? TimeZoneId { get; set; }
    public List<DailyHours> Days { get; set; } = []; 
    }
public sealed class DailyHours { 
    public DayOfWeek Day { get; set; } 
    public TimeOnly Open { get; set; } 
    public TimeOnly Close { get; set; } 
    }
