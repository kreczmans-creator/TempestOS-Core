using Tempest.Core.Timesheets;

namespace Tempest.Core.Tests.Timesheets;

/// <summary>`WP 19.0A`: <see cref="TimesheetWeek.WeekOf"/> — the one calendar rule this Work Package owns.</summary>
public sealed class TimesheetWeekTests
{
    [Fact]
    public void AMonday_IsItsOwnWeekStart()
    {
        var monday = new DateOnly(2026, 3, 2);
        Assert.Equal(DayOfWeek.Monday, monday.DayOfWeek);

        Assert.Equal(monday, TimesheetWeek.WeekOf(monday));
    }

    [Theory]
    [InlineData(2026, 3, 3, DayOfWeek.Tuesday)]
    [InlineData(2026, 3, 4, DayOfWeek.Wednesday)]
    [InlineData(2026, 3, 5, DayOfWeek.Thursday)]
    [InlineData(2026, 3, 6, DayOfWeek.Friday)]
    [InlineData(2026, 3, 7, DayOfWeek.Saturday)]
    [InlineData(2026, 3, 8, DayOfWeek.Sunday)]
    public void EveryOtherDayOfTheWeek_ResolvesToTheSameMonday(int year, int month, int day, DayOfWeek expectedDayOfWeek)
    {
        var date = new DateOnly(year, month, day);
        Assert.Equal(expectedDayOfWeek, date.DayOfWeek);

        Assert.Equal(new DateOnly(2026, 3, 2), TimesheetWeek.WeekOf(date));
    }

    /// <summary>
    /// 2025-01-01 is a Wednesday; its own ISO week starts on the Monday
    /// before it — 2024-12-30 — in the previous calendar year. This is the
    /// case that breaks a naive "day-of-year minus day-of-week" calculation.
    /// </summary>
    [Fact]
    public void ACrossYearBoundaryWeek_ResolvesToTheMondayInThePreviousYear()
    {
        var newYearsDay = new DateOnly(2025, 1, 1);
        Assert.Equal(DayOfWeek.Wednesday, newYearsDay.DayOfWeek);

        var weekStart = TimesheetWeek.WeekOf(newYearsDay);

        Assert.Equal(new DateOnly(2024, 12, 30), weekStart);
        Assert.Equal(DayOfWeek.Monday, weekStart.DayOfWeek);
    }

    /// <summary>The Sunday immediately before that same boundary is still in the old year's own last ISO week.</summary>
    [Fact]
    public void TheSundayBeforeTheBoundary_StaysInThePreviousWeek()
    {
        var lastSundayOfTheYear = new DateOnly(2024, 12, 29);
        Assert.Equal(DayOfWeek.Sunday, lastSundayOfTheYear.DayOfWeek);

        Assert.Equal(new DateOnly(2024, 12, 23), TimesheetWeek.WeekOf(lastSundayOfTheYear));
    }

    [Fact]
    public void WeekOf_IsIdempotent()
    {
        var date = new DateOnly(2026, 7, 15);
        var weekStart = TimesheetWeek.WeekOf(date);

        Assert.Equal(weekStart, TimesheetWeek.WeekOf(weekStart));
    }
}
