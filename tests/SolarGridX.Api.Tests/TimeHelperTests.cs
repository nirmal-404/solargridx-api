// Smart Solar Microgrid Trading System - time helper unit tests.
using SolarGridX.Api.Utilities;

namespace SolarGridX.Api.Tests;

public sealed class TimeHelperTests
{
    // Confirms that DateTime with Utc kind remains unchanged.
    [Fact]
    public void ToUtc_WithUtcKind_ReturnsSameUtcValue()
    {
        var utcNow = DateTime.UtcNow;
        var result = TimeHelper.ToUtc(utcNow);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(utcNow, result);
    }

    // Confirms that DateTime with Unspecified kind is treated as UTC.
    [Fact]
    public void ToUtc_WithUnspecifiedKind_TreatsAsUtc()
    {
        var unspecified = new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Unspecified);
        var result = TimeHelper.ToUtc(unspecified);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(2026, result.Year);
        Assert.Equal(9, result.Month);
        Assert.Equal(20, result.Day);
        Assert.Equal(10, result.Hour);
    }

    // Confirms that UtcNow returns a UTC DateTime.
    [Fact]
    public void UtcNow_ReturnsUtcKind()
    {
        var now = TimeHelper.UtcNow();
        Assert.Equal(DateTimeKind.Utc, now.Kind);
    }
}
