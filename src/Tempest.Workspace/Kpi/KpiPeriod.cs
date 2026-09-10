using System.Globalization;
using Tempest.Core.Timesheets;

namespace Tempest.Workspace.Kpi;

/// <summary>
/// The reporting window the Home cockpit's five KPI cards read against
/// (`WP 19.1B`): an inclusive <see cref="DateOnly"/> range, plus which
/// named preset — if any — produced it, so a persisted selection
/// ("this week") is re-anchored to whatever week it is now, rather than
/// frozen at the range it happened to compute on the day it was saved.
/// </summary>
public enum KpiPeriodPreset
{
    /// <summary>The ISO week (Monday–Sunday, <see cref="TimesheetWeek.WeekOf"/>) containing "today".</summary>
    ThisWeek,

    /// <summary>The ISO week immediately before <see cref="ThisWeek"/>.</summary>
    LastWeek,

    /// <summary>The calendar month containing "today".</summary>
    ThisMonth,

    /// <summary>The calendar month immediately before <see cref="ThisMonth"/>.</summary>
    LastMonth,

    /// <summary>The calendar quarter (Jan–Mar, Apr–Jun, Jul–Sep, Oct–Dec) containing "today".</summary>
    ThisQuarter,

    /// <summary>The calendar year containing "today".</summary>
    ThisYear,

    /// <summary>An explicit, caller-chosen <see cref="KpiPeriod.From"/>/<see cref="KpiPeriod.To"/> range.</summary>
    Custom,
}

/// <summary>
/// An inclusive <see cref="DateOnly"/> range naming one reporting window,
/// with the named preset that produced it (`WP 19.1B`).
/// </summary>
/// <remarks>
/// Construct through <see cref="ThisWeek"/>/<see cref="LastWeek"/>/
/// <see cref="ThisMonth"/>/<see cref="LastMonth"/>/<see cref="ThisQuarter"/>/
/// <see cref="ThisYear"/>/<see cref="Custom"/> — never the primary
/// constructor directly — each names exactly what it computes, and
/// <see cref="ForPreset"/> dispatches every non-<see cref="KpiPeriodPreset.Custom"/>
/// value to the matching one.
/// </remarks>
public sealed record KpiPeriod
{
    private KpiPeriod(DateOnly from, DateOnly to, KpiPeriodPreset preset)
    {
        if (to < from)
            throw new ArgumentException($"A period's own 'to' ({to:O}) must not precede its 'from' ({from:O}).", nameof(to));

        From = from;
        To = to;
        Preset = preset;
    }

    /// <summary>The first day of the range, inclusive.</summary>
    public DateOnly From { get; }

    /// <summary>The last day of the range, inclusive.</summary>
    public DateOnly To { get; }

    /// <summary>Which named preset produced this range, or <see cref="KpiPeriodPreset.Custom"/> for an explicit range.</summary>
    public KpiPeriodPreset Preset { get; }

    /// <summary>The number of calendar days this range spans, inclusive at both ends — never a week count, per `ADR-0150`'s own "never calendar days" caution about the denominator this feeds, not about this property itself.</summary>
    public int TotalDays => To.DayNumber - From.DayNumber + 1;

    /// <summary>Whether <paramref name="date"/> falls within this range, inclusive at both ends.</summary>
    public bool Contains(DateOnly date) => date >= From && date <= To;

    /// <summary>The ISO week (Monday–Sunday) containing <paramref name="today"/>.</summary>
    public static KpiPeriod ThisWeek(DateOnly today)
    {
        var start = TimesheetWeek.WeekOf(today);
        return new KpiPeriod(start, start.AddDays(6), KpiPeriodPreset.ThisWeek);
    }

    /// <summary>The ISO week immediately before <see cref="ThisWeek"/>.</summary>
    public static KpiPeriod LastWeek(DateOnly today)
    {
        var start = TimesheetWeek.WeekOf(today).AddDays(-7);
        return new KpiPeriod(start, start.AddDays(6), KpiPeriodPreset.LastWeek);
    }

    /// <summary>The calendar month containing <paramref name="today"/>.</summary>
    public static KpiPeriod ThisMonth(DateOnly today)
    {
        var start = new DateOnly(today.Year, today.Month, 1);
        return new KpiPeriod(start, start.AddMonths(1).AddDays(-1), KpiPeriodPreset.ThisMonth);
    }

    /// <summary>The calendar month immediately before <see cref="ThisMonth"/>.</summary>
    public static KpiPeriod LastMonth(DateOnly today)
    {
        var thisMonthStart = new DateOnly(today.Year, today.Month, 1);
        var start = thisMonthStart.AddMonths(-1);
        return new KpiPeriod(start, thisMonthStart.AddDays(-1), KpiPeriodPreset.LastMonth);
    }

    /// <summary>The calendar quarter containing <paramref name="today"/>.</summary>
    public static KpiPeriod ThisQuarter(DateOnly today)
    {
        var quarterStartMonth = (((today.Month - 1) / 3) * 3) + 1;
        var start = new DateOnly(today.Year, quarterStartMonth, 1);
        return new KpiPeriod(start, start.AddMonths(3).AddDays(-1), KpiPeriodPreset.ThisQuarter);
    }

    /// <summary>The calendar year containing <paramref name="today"/>.</summary>
    public static KpiPeriod ThisYear(DateOnly today)
    {
        var start = new DateOnly(today.Year, 1, 1);
        return new KpiPeriod(start, new DateOnly(today.Year, 12, 31), KpiPeriodPreset.ThisYear);
    }

    /// <summary>An explicit range, <paramref name="from"/> to <paramref name="to"/> inclusive.</summary>
    /// <exception cref="ArgumentException"><paramref name="to"/> precedes <paramref name="from"/>.</exception>
    public static KpiPeriod Custom(DateOnly from, DateOnly to) => new(from, to, KpiPeriodPreset.Custom);

    /// <summary>Computes the named <paramref name="preset"/>'s own range, anchored on <paramref name="today"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="preset"/> is <see cref="KpiPeriodPreset.Custom"/> — use <see cref="Custom"/>, which needs an explicit range this method cannot invent.</exception>
    public static KpiPeriod ForPreset(KpiPeriodPreset preset, DateOnly today) => preset switch
    {
        KpiPeriodPreset.ThisWeek => ThisWeek(today),
        KpiPeriodPreset.LastWeek => LastWeek(today),
        KpiPeriodPreset.ThisMonth => ThisMonth(today),
        KpiPeriodPreset.LastMonth => LastMonth(today),
        KpiPeriodPreset.ThisQuarter => ThisQuarter(today),
        KpiPeriodPreset.ThisYear => ThisYear(today),
        KpiPeriodPreset.Custom => throw new ArgumentException("A Custom period needs an explicit range; call KpiPeriod.Custom(from, to) instead.", nameof(preset)),
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown KpiPeriodPreset."),
    };

    /// <summary>
    /// Renders this period for <see cref="Tempest.Core.Settings.ISettingsProvider"/>
    /// persistence (`Cockpit.KpiPeriod`) — the preset's own name, so a restart
    /// re-anchors it to the week/month/quarter/year current at that
    /// restart, or <c>"Custom:{From}:{To}"</c> for an explicit range, which
    /// by its own nature never moves.
    /// </summary>
    public string Serialize() => Preset == KpiPeriodPreset.Custom
        ? $"Custom:{From:yyyy-MM-dd}:{To:yyyy-MM-dd}"
        : Preset.ToString();

    /// <summary>
    /// Parses <see cref="Serialize"/>'s own text back into a period,
    /// anchored on <paramref name="today"/> for a named preset. Never
    /// throws: a <see langword="null"/>/blank/malformed value — nothing
    /// ever persisted, an older build's own format, a hand-edited setting
    /// — degrades to <see cref="ThisWeek"/> rather than failing the whole
    /// Cockpit render over one bad setting value.
    /// </summary>
    public static KpiPeriod Parse(string? raw, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ThisWeek(today);

        if (raw.StartsWith("Custom:", StringComparison.Ordinal))
        {
            var parts = raw.Split(':');
            if (parts.Length == 3
                && DateOnly.TryParseExact(parts[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from)
                && DateOnly.TryParseExact(parts[2], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to)
                && to >= from)
            {
                return Custom(from, to);
            }

            return ThisWeek(today);
        }

        return Enum.TryParse<KpiPeriodPreset>(raw, out var preset) && preset != KpiPeriodPreset.Custom
            ? ForPreset(preset, today)
            : ThisWeek(today);
    }
}
