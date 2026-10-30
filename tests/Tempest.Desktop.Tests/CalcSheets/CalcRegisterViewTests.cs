using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Tempest.Calc.Library;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>The register of worksheet calculations at the top of Calculations → Calculation register (found missing by the showcase journey).</summary>
public sealed class CalcRegisterViewTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    private static CalcInstance Instance(string name, CalcInstanceStatus status, int minutes, string reference = "mech.bolted-joint@1.1.0", bool passed = true) =>
        new(Guid.NewGuid(), reference, "hash", null, name, "A", [], [], [new CalcCheckSnapshot("Yield", "0.8", "1", passed ? 0.826 : 1.2, passed)], [], [], status, null, null, "s.kreczman", At, At.AddMinutes(minutes));

    [AvaloniaFact]
    public void Lists_Newest_First_Without_Archives_And_Counts_Each_Status()
    {
        var view = new CalcRegisterView();
        var archive = Instance("legacy run", CalcInstanceStatus.Draft, 9, CalcInstance.LegacyReferencePrefix + "calc.beam@1");

        view.ShowEntries([Instance("Older", CalcInstanceStatus.Issued, 1), Instance("Newer", CalcInstanceStatus.Draft, 5, passed: false), archive], "P-DEMO Showcase", r => r.StartsWith("mech.", StringComparison.Ordinal) ? "Bolted joint" : null);

        Assert.Equal(["Newer", "Older"], view.Shown.Select(i => i.Name));
        var texts = view.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains($"{CalcRegisterView.Heading} — P-DEMO Showcase", texts);
        Assert.Contains("2 calculation(s): 1 issued, 0 checked, 1 draft, 0 superseded.", texts);
        Assert.Contains("Bolted joint", texts);
        Assert.Contains("ISSUED", texts);
        Assert.Contains("FAIL u = 1.20", texts);
        Assert.Contains("PASS u = 0.83", texts);
    }

    private static CalcInstance WithChecks(string name, params CalcCheckSnapshot[] checks) =>
        Instance(name, CalcInstanceStatus.Draft, 1) with { Checks = checks };

    /// <summary>C-2 verification F5: the register printed "PASS u = 0.00" for a calculation whose governing check is a yes/no check (a fit class, a separation test): there is no ratio to print.</summary>
    [AvaloniaFact]
    public void A_Yes_No_Governing_Check_Shows_Pass_Or_Fail_And_No_Utilisation_And_A_Ratio_Check_Still_Shows_Its_Utilisation()
    {
        var view = new CalcRegisterView();
        var fit = new CalcCheckSnapshot("Fit class", "H7/g6", "clearance", 0.0, true, HasUtilisation: false);
        var separation = new CalcCheckSnapshot("Separation", "no", "yes", 1e9, false, HasUtilisation: false);
        var ratio = new CalcCheckSnapshot("Yield", "0.8", "1", 0.826, true);
        view.ShowEntries([WithChecks("Fit", fit), WithChecks("Gap", ratio, separation), WithChecks("Beam", ratio)], null, _ => null);

        var texts = view.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("PASS", texts);
        Assert.Contains("FAIL", texts);
        Assert.Contains("PASS u = 0.83", texts);
        Assert.DoesNotContain("PASS u = 0.00", texts);
        Assert.DoesNotContain("FAIL u = —", texts);
    }

    /// <summary>A calculation saved before the field existed has no HasUtilisation: it reads as a ratio check, as it was shown.</summary>
    [Fact]
    public void A_Check_Saved_Without_The_Field_Reads_As_A_Ratio_Check_And_A_Yes_No_Check_Round_Trips()
    {
        var old = System.Text.Json.JsonSerializer.Deserialize<CalcCheckSnapshot>("""{"Name":"Yield","Demand":"0.8","Capacity":"1","Utilisation":0.826,"Passed":true}""")!;
        Assert.True(old.HasUtilisation);

        var yesNo = new CalcCheckSnapshot("Fit class", "H7/g6", "clearance", 0.0, true, HasUtilisation: false);
        var back = System.Text.Json.JsonSerializer.Deserialize<CalcCheckSnapshot>(System.Text.Json.JsonSerializer.Serialize(yesNo))!;
        Assert.False(back.HasUtilisation);
    }

    [AvaloniaFact]
    public void Open_Asks_For_That_Calculation()
    {
        var view = new CalcRegisterView();
        var wanted = Instance("CALC-M01", CalcInstanceStatus.Issued, 1);
        view.ShowEntries([Instance("Other", CalcInstanceStatus.Draft, 2), wanted], null, _ => null);
        Guid? opened = null;
        view.OpenRequested += id => opened = id;

        var button = view.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == $"{CalcRegisterView.OpenCaption} CALC-M01");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(wanted.Id, opened);
    }

    [AvaloniaFact]
    public void An_Empty_Register_Says_Where_Calculations_Come_From_And_A_Long_One_Is_Capped()
    {
        var view = new CalcRegisterView();
        view.ShowEntries([], null, _ => null);
        Assert.Contains(view.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.StartsWith("No worksheet calculations yet.", StringComparison.Ordinal) == true);

        view.ShowEntries([.. Enumerable.Range(0, CalcRegisterView.MaxListed + 5).Select(n => Instance($"C{n}", CalcInstanceStatus.Draft, n))], null, _ => null);
        Assert.Equal(CalcRegisterView.MaxListed, view.Shown.Count);
        Assert.Contains(view.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.EndsWith($"The newest {CalcRegisterView.MaxListed} are listed.", StringComparison.Ordinal) == true);
    }
}
