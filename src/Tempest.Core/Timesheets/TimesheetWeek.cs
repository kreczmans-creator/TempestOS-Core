namespace Tempest.Core.Timesheets;

/// <summary>
/// The one calendar rule `WP 19.0A` needs: which Monday a date's own week
/// starts on (ISO 8601 week numbering — Monday, never Sunday). No other
/// calendar logic belongs here or anywhere else in this Work Package.
/// </summary>
public static class TimesheetWeek
{
    /// <summary>The Monday on or before <paramref name="date"/> — the start of <paramref name="date"/>'s own ISO week.</summary>
    public static DateOnly WeekOf(DateOnly date)
    {
        // DayOfWeek: Sunday = 0 .. Saturday = 6. Shifting so Monday = 0
        // gives the number of days to step back to reach that week's own
        // Monday, correctly wrapping a Sunday back six days into the
        // previous week rather than forward.
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;

        return date.AddDays(-daysSinceMonday);
    }
}
