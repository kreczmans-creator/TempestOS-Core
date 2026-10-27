using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>Shot-fix A: what the screenshots of every worked example found wrong on the worksheet screens, each fixed and held here.</summary>
public sealed class WorksheetShotFixTests
{
    private static CalcInstance Instance(CalcCatalogueEntry entry, CalcInstanceStatus status) => new(
        Guid.NewGuid(), entry.Reference, entry.ContentHash, null, "CALC-1", "A",
        [CalcInputValue.Quantity("f_y", 355, "MPa", CalcProvenance.Drawing("D1"))],
        [], [], [], [], status, status == CalcInstanceStatus.Draft ? null : Guid.NewGuid(), null, "s.k", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
        IssuedAt: status == CalcInstanceStatus.Issued ? DateTimeOffset.UnixEpoch : null);

    [AvaloniaFact]
    public void Item2_A_Reference_Picker_Is_Read_Only_On_An_Issued_Form_Like_Every_Other_Field()
    {
        var catalogue = new CalcCatalogue([new Tempest.Calc.Structural.MemberChecksCalculator()]);
        var entry = catalogue.Get("struct.en1993-member");
        var view = new CalcSheetView();
        view.ShowCatalogue(catalogue.Current());

        // A draft: the picker is usable once it has records.
        var draft = Instance(entry, CalcInstanceStatus.Draft);
        view.ShowTemplate(entry, draft);
        view.SetReferenceOptions("Materials", ["S355 — Structural steel"]);
        Assert.True(view.IsReferencePickerEnabled("Materials"));

        // Issued in place (the form is not rebuilt): the picker goes read-only with the fields.
        view.ShowInstance(draft with { Status = CalcInstanceStatus.Issued, IssuedAt = DateTimeOffset.UnixEpoch });
        Assert.True(view.IsReadOnly);
        Assert.False(view.IsReferencePickerEnabled("Materials"));

        // Opened as issued, with the records already listed: read-only from the start.
        view.ShowTemplate(entry, Instance(entry, CalcInstanceStatus.Issued));
        Assert.False(view.IsReferencePickerEnabled("Materials"));
        view.SetReferenceOptions("Materials", ["S355 — Structural steel"]);
        Assert.False(view.IsReferencePickerEnabled("Materials"));
        var picker = view.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == CalcSheetView.ReferencePickerName("Materials"));
        Assert.False(picker.IsEnabled);
    }

    private static (CalcSheetView View, Window Window) Shown(double width = 1100)
    {
        var catalogue = new CalcCatalogue([new TestWorksheetCalculator()]);
        var view = new CalcSheetView();
        view.InputsChanged += inputs => view.ShowRun(CalcRunner.Run(catalogue.Get("test.desktop-stress").Calculator, inputs));
        view.ShowCatalogue(catalogue.Current());
        var window = new Window { Width = width, Height = 900, Content = view };
        window.Show();
        view.SelectTemplate("test.desktop-stress");
        return (view, window);
    }

    [AvaloniaFact]
    public void Item3_A_Long_Blocked_Line_Wraps_Inside_The_Results_Panel()
    {
        var (view, window) = Shown();
        var message = string.Concat(Enumerable.Repeat("enter at least one row of the layers table, from the inside face to the outside face; ", 4));
        view.ShowRun(CalcRunResult.Blocked([new CalcNotice("input.layers", CalcNoticeSeverity.Block, message)]));
        window.UpdateLayout();

        var block = view.GoverningBlock;
        Assert.StartsWith("Blocked — enter at least one row", view.GoverningText, StringComparison.Ordinal);
        Assert.True(block.Bounds.Width <= window.Width - 380, $"the governing line is {block.Bounds.Width} px wide beside a 380 px catalogue in a {window.Width} px window");
        Assert.True(block.Bounds.Height > block.FontSize * 2.5, $"the governing line did not wrap (height {block.Bounds.Height})");
    }

    [AvaloniaFact]
    public void Item4_A_Governing_Yes_No_Check_Shows_A_Plain_Verdict_And_No_Empty_Bar()
    {
        var (view, _) = Shown();
        var yesNo = new CalcCheck("Fault current against the disconnection current", "If below the instantaneous tripping current", "disconnection within t_req", double.PositiveInfinity, Passed: false, HasUtilisation: false);
        var ratio = new CalcCheck("Loop impedance against the maximum", "1.4034 Ω", "1.3656 Ω", 1.03, Passed: false);
        view.ShowRun(new CalcRunResult([], [], [yesNo, ratio], [], [], []));

        Assert.Equal("FAIL — Fault current against the disconnection current", view.GoverningText);
        Assert.False(view.IsUtilisationBarShown);
        Assert.Equal("FAIL", view.GoverningVerdictMark);

        // A passing yes/no check governs only when every check passes and none has a ratio above it.
        view.ShowRun(new CalcRunResult([], [], [yesNo with { Passed = true, Utilisation = 0 }], [], [], []));
        Assert.Equal("PASS", view.GoverningVerdictMark);
        Assert.False(view.IsUtilisationBarShown);

        // A ratio check governs: the bar, and no verdict mark.
        view.ShowRun(new CalcRunResult([], [], [ratio], [], [], []));
        Assert.True(view.IsUtilisationBarShown);
        Assert.Null(view.GoverningVerdictMark);
    }
}
