using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Demonstrates `WP 10.5C`'s own new, real, standalone visual-language
/// primitives directly — <see cref="DisciplineColors"/> (the "engineering
/// colour language") and <see cref="CockpitCardControl.AddKpiRow"/> (real
/// progress-bar KPI rendering) — neither needs a real
/// <see cref="Tempest.App.Workspace.WorkspaceHost"/> to prove, unlike
/// their own real consumers (<see cref="RibbonViewTests"/>,
/// <see cref="CockpitViewHonestyTests"/>), which do.
/// </summary>
public sealed class CommercialUxTests
{
    [AvaloniaFact]
    public void DisciplineColors_TheSixRealDisciplines_EachResolveToADistinctColour()
    {
        var disciplines = new[] { "Mechanical", "Requirements", "Calculations", "Verification", "Documents", "Manufacturing" };
        var brushes = disciplines.Select(DisciplineColors.Resolve).ToList();

        Assert.Equal(disciplines.Length, brushes.Distinct().Count());
    }

    [AvaloniaFact]
    public void DisciplineColors_MatchesByRealCategorySubstring_NotExactEquality()
    {
        // Real Navigation area titles contain, but do not equal, their own
        // Category word (`RibbonView.SelectTabForArea`'s own identical,
        // already-established precedent) — `DisciplineColors` must match
        // the same way, or a real area title would silently fall back to
        // the neutral default.
        Assert.Equal(DisciplineColors.Resolve("Mechanical"), DisciplineColors.Resolve("Mechanical Product Structure"));
        Assert.Equal(DisciplineColors.Resolve("Requirements"), DisciplineColors.Resolve("Requirements Management"));
    }

    [AvaloniaFact]
    public void DisciplineColors_UnrecognisedCategory_FallsBackToNeutral_NeverThrows()
    {
        var brush = DisciplineColors.Resolve("Some Future Discipline Nobody Has Named Yet");
        Assert.NotNull(brush);

        // A null category (`RibbonView`'s own `Category ?? "General"`
        // fallback never actually reaches this method with a real null in
        // practice, but the method itself never throws for one either) —
        // resolves to the identical neutral default, never `null` itself.
        Assert.NotNull(DisciplineColors.Resolve(null));
    }

    [AvaloniaFact]
    public void CockpitCardControl_AddKpiRow_WithAPercent_RendersARealProgressBar()
    {
        var card = new CockpitCardControl("📋", "Requirements KPIs");
        card.AddKpiRow("Verification Coverage", "60% (3/5)", 60);

        var bar = card.GetLogicalDescendants().OfType<ProgressBar>().Single();
        Assert.Equal(60, bar.Value);
        Assert.Equal(0, bar.Minimum);
        Assert.Equal(100, bar.Maximum);
    }

    [AvaloniaFact]
    public void CockpitCardControl_AddKpiRow_WithNoPercent_RendersPlainTextOnly_NeverAFabricatedBar()
    {
        var card = new CockpitCardControl("📋", "Requirements KPIs");
        card.AddKpiRow("Total Requirements", "5", percent: null);

        Assert.Empty(card.GetLogicalDescendants().OfType<ProgressBar>());
        Assert.Contains(card.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Total Requirements: 5");
    }

    [AvaloniaFact]
    public void CockpitCardControl_AddKpiRow_HighVsLowPercent_UseDifferentColours()
    {
        var healthy = new CockpitCardControl("📋", "A");
        healthy.AddKpiRow("X", "90%", 90);
        var blocked = new CockpitCardControl("📋", "B");
        blocked.AddKpiRow("X", "10%", 10);

        var healthyBar = healthy.GetLogicalDescendants().OfType<ProgressBar>().Single();
        var blockedBar = blocked.GetLogicalDescendants().OfType<ProgressBar>().Single();

        Assert.NotEqual(healthyBar.Foreground, blockedBar.Foreground);
    }
}
