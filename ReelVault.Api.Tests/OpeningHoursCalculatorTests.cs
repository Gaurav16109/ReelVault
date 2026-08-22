using ReelVault.Shared;

namespace ReelVault.Api.Tests;

public class OpeningHoursCalculatorTests
{
    [Fact]
    public void IsOpenNow_NoPeriods_ReturnsNull()
    {
        // Act
        var result = OpeningHoursCalculator.IsOpenNow(null, new DateTime(2026, 8, 15, 12, 0, 0));

        // Assert: "we don't know" - never guess.
        Assert.Null(result);
    }

    [Fact]
    public void IsOpenNow_EmptyPeriodsList_ReturnsNull()
    {
        // Act
        var result = OpeningHoursCalculator.IsOpenNow([], new DateTime(2026, 8, 15, 12, 0, 0));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void IsOpenNow_SameDayWithinPeriod_IsTrue()
    {
        // Arrange: Saturday 9 AM - 10 PM.
        var periods = new List<OpeningPeriod>
        {
            new() { OpenDay = DayOfWeek.Saturday, OpenTime = new TimeOnly(9, 0), CloseDay = DayOfWeek.Saturday, CloseTime = new TimeOnly(22, 0) }
        };

        // Act: Saturday 2026-08-15 at 3:00 PM.
        var result = OpeningHoursCalculator.IsOpenNow(periods, new DateTime(2026, 8, 15, 15, 0, 0));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsOpenNow_SameDayBeforeOpening_IsFalse()
    {
        // Arrange: Saturday 9 AM - 10 PM.
        var periods = new List<OpeningPeriod>
        {
            new() { OpenDay = DayOfWeek.Saturday, OpenTime = new TimeOnly(9, 0), CloseDay = DayOfWeek.Saturday, CloseTime = new TimeOnly(22, 0) }
        };

        // Act: Saturday 2026-08-15 at 7:00 AM - before opening.
        var result = OpeningHoursCalculator.IsOpenNow(periods, new DateTime(2026, 8, 15, 7, 0, 0));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsOpenNow_SameDayAfterClosing_IsFalse()
    {
        // Arrange: Saturday 9 AM - 10 PM.
        var periods = new List<OpeningPeriod>
        {
            new() { OpenDay = DayOfWeek.Saturday, OpenTime = new TimeOnly(9, 0), CloseDay = DayOfWeek.Saturday, CloseTime = new TimeOnly(22, 0) }
        };

        // Act: Saturday 2026-08-15 at 11:00 PM - after closing.
        var result = OpeningHoursCalculator.IsOpenNow(periods, new DateTime(2026, 8, 15, 23, 0, 0));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsOpenNow_WrongDayOfWeek_IsFalse()
    {
        // Arrange: only open Saturday.
        var periods = new List<OpeningPeriod>
        {
            new() { OpenDay = DayOfWeek.Saturday, OpenTime = new TimeOnly(9, 0), CloseDay = DayOfWeek.Saturday, CloseTime = new TimeOnly(22, 0) }
        };

        // Act: Sunday 2026-08-16 at 3:00 PM.
        var result = OpeningHoursCalculator.IsOpenNow(periods, new DateTime(2026, 8, 16, 15, 0, 0));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsOpenNow_SpansMidnight_IsTrueLateAtNightOnOpenDay()
    {
        // Arrange: Friday 8:30 PM - Saturday 1:00 AM (a late-night bar).
        var periods = new List<OpeningPeriod>
        {
            new() { OpenDay = DayOfWeek.Friday, OpenTime = new TimeOnly(20, 30), CloseDay = DayOfWeek.Saturday, CloseTime = new TimeOnly(1, 0) }
        };

        // Act: Friday 2026-08-14 at 11:00 PM.
        var result = OpeningHoursCalculator.IsOpenNow(periods, new DateTime(2026, 8, 14, 23, 0, 0));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsOpenNow_SpansMidnight_IsTrueEarlyMorningOnCloseDay()
    {
        // Arrange: Friday 8:30 PM - Saturday 1:00 AM.
        var periods = new List<OpeningPeriod>
        {
            new() { OpenDay = DayOfWeek.Friday, OpenTime = new TimeOnly(20, 30), CloseDay = DayOfWeek.Saturday, CloseTime = new TimeOnly(1, 0) }
        };

        // Act: Saturday 2026-08-15 at 12:30 AM - still within the overnight span.
        var result = OpeningHoursCalculator.IsOpenNow(periods, new DateTime(2026, 8, 15, 0, 30, 0));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsOpenNow_SpansMidnight_IsFalseAfterCloseTimeOnCloseDay()
    {
        // Arrange: Friday 8:30 PM - Saturday 1:00 AM.
        var periods = new List<OpeningPeriod>
        {
            new() { OpenDay = DayOfWeek.Friday, OpenTime = new TimeOnly(20, 30), CloseDay = DayOfWeek.Saturday, CloseTime = new TimeOnly(1, 0) }
        };

        // Act: Saturday 2026-08-15 at 9:00 AM - well past the 1 AM close.
        var result = OpeningHoursCalculator.IsOpenNow(periods, new DateTime(2026, 8, 15, 9, 0, 0));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsOpenNow_MultiplePeriodsAcrossTheWeek_ChecksAllOfThem()
    {
        // Arrange: open Mon-Fri 9-5, and separately Saturday 10-2.
        var periods = new List<OpeningPeriod>
        {
            new() { OpenDay = DayOfWeek.Monday, OpenTime = new TimeOnly(9, 0), CloseDay = DayOfWeek.Monday, CloseTime = new TimeOnly(17, 0) },
            new() { OpenDay = DayOfWeek.Tuesday, OpenTime = new TimeOnly(9, 0), CloseDay = DayOfWeek.Tuesday, CloseTime = new TimeOnly(17, 0) },
            new() { OpenDay = DayOfWeek.Saturday, OpenTime = new TimeOnly(10, 0), CloseDay = DayOfWeek.Saturday, CloseTime = new TimeOnly(14, 0) }
        };

        // Act: Saturday 2026-08-15 at 11:00 AM.
        var result = OpeningHoursCalculator.IsOpenNow(periods, new DateTime(2026, 8, 15, 11, 0, 0));

        // Assert
        Assert.True(result);
    }
}
