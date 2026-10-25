using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>
/// The v0.26.0 line holds the work of three lines (Addenda A and B, and the legacy-calculator replacement formerly planned as v0.27.0). Templates
/// that look alike but are different calculations all stay: the picker must list every one, and withhold only the two lifting worksheets while
/// their feature is off (ADR-0173).
/// </summary>
public sealed class MergedCatalogueTests
{
    /// <summary>Pairs that overlap in subject and are kept side by side (ADR-0167 Amendment 2).</summary>
    private static readonly string[] OverlappingPairs =
    [
        "mech.bearing-life", "mech.bearing-life-basic",
        "mech.shaft", "mech.shaft-stress",
        "struct.connection-ec3", "mech.weld-group", "struct.fillet-weld",
        "bs.room-heat-loss", "bs.plane-wall",
        "struct.lifting", "mech.lifting-lug",
    ];

    private static readonly string[] Lifting = ["mech.lifting-lug", "struct.lifting"];

    private static IReadOnlyList<string> Listed(CalcSheetView view) =>
    [
        .. view.GetLogicalDescendants().OfType<TreeView>().Single(t => AutomationProperties.GetName(t) == CalcSheetView.CatalogueAutomationName)
            .Items.OfType<TreeViewItem>().SelectMany(group => group.Items.OfType<TreeViewItem>()).Select(leaf => leaf.Tag).OfType<CalcCatalogueEntry>().Select(e => e.Template.Id),
    ];

    [AvaloniaFact]
    public void The_Worksheet_Picker_Lists_Every_Template_And_Withholds_Only_The_Two_Lifting_Ones_While_The_Feature_Is_Off()
    {
        var all = CalcCalculatorComposition.All().Select(c => c.Template.Id).ToList();
        Assert.Equal(54, all.Count);
        Assert.Equal(all.Count, all.Distinct(StringComparer.Ordinal).Count());
        Assert.All(OverlappingPairs, id => Assert.Contains(id, all));

        var off = new CalcCatalogue(CalcCalculatorComposition.All());
        var offView = new CalcSheetView();
        offView.ShowCatalogue(off.Current());
        var offListed = Listed(offView);
        Assert.Equal(all.Except(Lifting).Order(StringComparer.Ordinal), offListed.Order(StringComparer.Ordinal));

        var on = new CalcCatalogue(CalcCalculatorComposition.All(), new CalcFeatureFlags([CalcFeatures.Lifting]));
        var onView = new CalcSheetView();
        onView.ShowCatalogue(on.Current());
        Assert.Equal(all.Order(StringComparer.Ordinal), Listed(onView).Order(StringComparer.Ordinal));
    }

    [AvaloniaFact]
    public void The_Overlapping_Templates_Open_As_Different_Calculations()
    {
        var catalogue = new CalcCatalogue(CalcCalculatorComposition.All(), new CalcFeatureFlags([CalcFeatures.Lifting]));
        var titles = OverlappingPairs.Select(id => catalogue.Find(id)!.Template.Title).ToList();
        Assert.Equal(titles.Count, titles.Distinct(StringComparer.Ordinal).Count());
    }
}
