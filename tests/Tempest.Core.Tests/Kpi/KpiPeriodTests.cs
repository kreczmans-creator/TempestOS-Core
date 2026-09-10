using Tempest.Workspace.Kpi;

namespace Tempest.Core.Tests.Kpi;

/// <summary>
/// <see cref="KpiPeriod"/> (`WP 19.1B`): the six named presets, an
/// explicit custom range, and the settings-provider round trip
/// <see cref="KpiPeriodSetting"/> uses to persist the Home cockpit's
/// own selection.
/// </summary>
public sealed class KpiPeriodTests
{
    // A fixed Wednesday, so "this week"/"this month" etc. never depend on
    // the day the test happens to run.
    private static readonly DateOnly Today = new(2026, 3, 11);

    [Fact]
    public void ThisWeek_IsTheIsoWeek_MondayToSunday()
    {
        var period = KpiPeriod.ThisWeek(Today);

        Assert.Equal(new DateOnly(2026, 3, 9), period.From); // the Monday on or before 2026-03-11
        Assert.Equal(new DateOnly(2026, 3, 15), period.To); // Sunday, six days later
        Assert.Equal(KpiPeriodPreset.ThisWeek, period.Preset);
        Assert.Equal(7, period.TotalDays);
    }

    [Fact]
    public void LastWeek_IsTheIsoWeekImmediatelyBefore()
    {
        var period = KpiPeriod.LastWeek(Today);

        Assert.Equal(new DateOnly(2026, 3, 2), period.From);
        Assert.Equal(new DateOnly(2026, 3, 8), period.To);
        Assert.Equal(7, period.TotalDays);
    }

    [Fact]
    public void ThisMonth_IsTheFirstToTheLastDayOfTheCalendarMonth()
    {
        var period = KpiPeriod.ThisMonth(Today);

        Assert.Equal(new DateOnly(2026, 3, 1), period.From);
        Assert.Equal(new DateOnly(2026, 3, 31), period.To); // March has 31 days
        Assert.Equal(31, period.TotalDays);
    }

    [Fact]
    public void LastMonth_IsTheCalendarMonthImmediatelyBefore()
    {
        var period = KpiPeriod.LastMonth(Today);

        Assert.Equal(new DateOnly(2026, 2, 1), period.From);
        Assert.Equal(new DateOnly(2026, 2, 28), period.To); // 2026 is not a leap year
        Assert.Equal(28, period.TotalDays);
    }

    [Fact]
    public void ThisQuarter_IsTheThreeMonthBlockContainingToday()
    {
        var period = KpiPeriod.ThisQuarter(Today); // March -> Q1: Jan-Mar

        Assert.Equal(new DateOnly(2026, 1, 1), period.From);
        Assert.Equal(new DateOnly(2026, 3, 31), period.To);
    }

    [Fact]
    public void ThisYear_IsJanuaryFirstToDecemberThirtyFirst()
    {
        var period = KpiPeriod.ThisYear(Today);

        Assert.Equal(new DateOnly(2026, 1, 1), period.From);
        Assert.Equal(new DateOnly(2026, 12, 31), period.To);
        Assert.Equal(365, period.TotalDays);
    }

    [Fact]
    public void Custom_AnExplicitRange_IsHeldExactly()
    {
        var period = KpiPeriod.Custom(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 20));

        Assert.Equal(new DateOnly(2026, 1, 5), period.From);
        Assert.Equal(new DateOnly(2026, 1, 20), period.To);
        Assert.Equal(KpiPeriodPreset.Custom, period.Preset);
        Assert.Equal(16, period.TotalDays); // inclusive at both ends: 20 - 5 + 1
    }

    [Fact]
    public void Custom_WithToBeforeFrom_Throws() =>
        Assert.Throws<ArgumentException>(() => KpiPeriod.Custom(new DateOnly(2026, 1, 20), new DateOnly(2026, 1, 5)));

    [Fact]
    public void Contains_IsInclusiveAtBothEnds()
    {
        var period = KpiPeriod.Custom(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 10));

        Assert.True(period.Contains(new DateOnly(2026, 1, 5)));
        Assert.True(period.Contains(new DateOnly(2026, 1, 10)));
        Assert.True(period.Contains(new DateOnly(2026, 1, 7)));
        Assert.False(period.Contains(new DateOnly(2026, 1, 4)));
        Assert.False(period.Contains(new DateOnly(2026, 1, 11)));
    }

    [Fact]
    public void ForPreset_DispatchesToTheMatchingFactory()
    {
        Assert.Equal(KpiPeriod.ThisMonth(Today), KpiPeriod.ForPreset(KpiPeriodPreset.ThisMonth, Today));
    }

    [Fact]
    public void ForPreset_Custom_Throws() =>
        Assert.Throws<ArgumentException>(() => KpiPeriod.ForPreset(KpiPeriodPreset.Custom, Today));

    // ----------------------------------------------------------------
    // Serialize/Parse — the ISettingsProvider round trip
    // ----------------------------------------------------------------

    [Theory]
    [InlineData(KpiPeriodPreset.ThisWeek)]
    [InlineData(KpiPeriodPreset.LastWeek)]
    [InlineData(KpiPeriodPreset.ThisMonth)]
    [InlineData(KpiPeriodPreset.LastMonth)]
    [InlineData(KpiPeriodPreset.ThisQuarter)]
    [InlineData(KpiPeriodPreset.ThisYear)]
    public void Serialize_ThenParse_OfANamedPreset_ReAnchorsOnWhateverTodayParseIsGiven(KpiPeriodPreset preset)
    {
        var saved = KpiPeriod.ForPreset(preset, Today);
        var serialized = saved.Serialize();
        Assert.Equal(preset.ToString(), serialized); // the preset's own name, not a literal date range

        // Parsed back a month later: the range moves with "today" — this
        // is the whole point of persisting the preset rather than the
        // dates it happened to compute on the day it was saved.
        var later = Today.AddMonths(1);
        var reparsed = KpiPeriod.Parse(serialized, later);
        Assert.Equal(KpiPeriod.ForPreset(preset, later), reparsed);
    }

    [Fact]
    public void Serialize_ThenParse_OfACustomRange_NeverMoves()
    {
        var saved = KpiPeriod.Custom(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 20));
        var serialized = saved.Serialize();
        Assert.Equal("Custom:2026-01-05:2026-01-20", serialized);

        var reparsed = KpiPeriod.Parse(serialized, Today.AddYears(1));
        Assert.Equal(saved, reparsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NotARealPreset")]
    [InlineData("Custom:not-a-date:2026-01-20")]
    [InlineData("Custom:2026-01-20:2026-01-05")] // to before from
    public void Parse_OfAnythingUnreadable_DegradesToThisWeek_NeverThrows(string? raw)
    {
        var period = KpiPeriod.Parse(raw, Today);
        Assert.Equal(KpiPeriod.ThisWeek(Today), period);
    }
}
