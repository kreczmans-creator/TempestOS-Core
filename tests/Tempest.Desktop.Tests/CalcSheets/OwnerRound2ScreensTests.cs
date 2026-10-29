using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>
/// The owner's comments of 2026-10-29 on the worksheet screens: fastener records picked by thread size then grade, in
/// numeric order; input labels as "symbol — name" with the description on hover; catalogue rows by title only.
/// </summary>
public sealed class OwnerRound2ScreensTests
{
    private static CalcInstance Instance(CalcCatalogueEntry entry, CalcInstanceStatus status) => new(
        Guid.NewGuid(), entry.Reference, entry.ContentHash, null, "CALC-1", "A",
        [], [], [], [], [], status, status == CalcInstanceStatus.Draft ? null : Guid.NewGuid(), null, "s.k", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
        IssuedAt: status == CalcInstanceStatus.Issued ? DateTimeOffset.UnixEpoch : null);

    // ------------------------------------------------------------------ comments 1 and 3: the fastener pickers

    /// <summary>The fastener records as the coordinator lists them: by size, then grade, with a thread-only record and one whose size cannot be read.</summary>
    private static IReadOnlyList<CalcSheetView.ReferenceChoice> Fasteners() =>
    [
        new("M3 x 0.5 - 4.6", "M3 x 0.5", "4.6"),
        new("M3 x 0.5 - 8.8", "M3 x 0.5", "8.8"),
        new("M3 x 0.5 - A4-80", "M3 x 0.5", "A4-80"),
        new("M12 x 1.75 - 8.8", "M12 x 1.75", "8.8"),
        new("M12 x 1.75 - 10.9", "M12 x 1.75", "10.9"),
        new("M12 x 1.75 — Bolt", "M12 x 1.75", null),
        new("M27 x 3 - 8.8", "M27 x 3", "8.8"),
        new("M30 x 3.5 - 8.8", "M30 x 3.5", "8.8"),
        new("FX-BOLT-1 — FX-A"),
    ];

    private static (CalcSheetView View, CalcCatalogueEntry Entry, List<int> Picked) BoltedJoint()
    {
        var catalogue = new CalcCatalogue([new Tempest.Calc.Mechanical.BoltedJointCalculator()]);
        var entry = catalogue.Get(Tempest.Calc.Mechanical.BoltedJointCalculator.Id);
        var view = new CalcSheetView();
        var picked = new List<int>();
        view.ReferencePicked += (library, index) =>
        {
            if (library == "Fasteners")
                picked.Add(index);
        };
        view.ShowCatalogue(catalogue.Current());
        view.ShowTemplate(entry, Instance(entry, CalcInstanceStatus.Draft));
        return (view, entry, picked);
    }

    private static ComboBox Picker(CalcSheetView view, string name) =>
        view.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == name);

    private static List<string> Items(ComboBox picker) => picker.Items.Cast<object>().Select(o => o.ToString()!).ToList();

    [AvaloniaFact]
    public void The_Fastener_Record_Is_Picked_By_Thread_Size_Then_Grade_And_The_Pair_Selects_The_One_Record()
    {
        var (view, _, picked) = BoltedJoint();
        var choices = Fasteners();
        view.SetReferenceOptions("Fasteners", choices);

        Assert.True(view.IsReferencePickerSplit("Fasteners"));
        Assert.False(Picker(view, CalcSheetView.ReferencePickerName("Fasteners")).IsVisible);
        var size = Picker(view, CalcSheetView.ReferenceSizePickerName("Fasteners"));
        var grade = Picker(view, CalcSheetView.ReferenceGradePickerName("Fasteners"));

        // The size list is sizes only, smallest first, with no grade in it; a record whose size cannot be read is listed under its own label.
        Assert.Equal(["M3 x 0.5", "M12 x 1.75", "M27 x 3", "M30 x 3.5", "FX-BOLT-1 — FX-A"], Items(size));
        Assert.Equal("Pick a thread size (5)", size.PlaceholderText);
        Assert.True(size.IsEnabled);
        Assert.False(grade.IsEnabled);

        // A size lists only its own grades; picking one reports the index of that one record.
        size.SelectedIndex = 1;
        Assert.Equal(["8.8", "10.9", CalcSheetView.NoGradeText], Items(grade));
        Assert.Equal("Pick a grade (3)", grade.PlaceholderText);
        Assert.True(grade.IsEnabled);
        Assert.Empty(picked);
        grade.SelectedIndex = 1;
        Assert.Equal([4], picked);
        Assert.Equal("M12 x 1.75 - 10.9", choices[picked[^1]].Label);

        // Another size keeps the grade where it has it, and picks that record at once.
        size.SelectedIndex = 0;
        Assert.Equal(["4.6", "8.8", "A4-80"], Items(grade));
        Assert.Equal([4], picked);

        grade.SelectedIndex = 1;
        size.SelectedIndex = 3;
        Assert.Equal(["8.8"], Items(grade));
        Assert.Equal("8.8", grade.SelectedItem);
        Assert.Equal("M30 x 3.5 - 8.8", choices[picked[^1]].Label);

        // A size with one record picks it.
        size.SelectedIndex = 4;
        Assert.Equal("FX-BOLT-1 — FX-A", choices[picked[^1]].Label);
    }

    [AvaloniaFact]
    public void Both_Fastener_Pickers_Are_Locked_On_An_Issued_Form()
    {
        var (view, entry, _) = BoltedJoint();
        view.SetReferenceOptions("Fasteners", Fasteners());
        var size = Picker(view, CalcSheetView.ReferenceSizePickerName("Fasteners"));
        var grade = Picker(view, CalcSheetView.ReferenceGradePickerName("Fasteners"));
        size.SelectedIndex = 0;
        Assert.True(view.IsReferencePickerEnabled("Fasteners"));
        Assert.True(grade.IsEnabled);

        // Issued in place: both go read-only with the fields.
        view.ShowInstance(view.CurrentInstance! with { Status = CalcInstanceStatus.Issued, IssuedAt = DateTimeOffset.UnixEpoch });
        Assert.False(view.IsReferencePickerEnabled("Fasteners"));
        Assert.False(size.IsEnabled);
        Assert.False(grade.IsEnabled);

        // Opened as issued, with the records listed again: read-only from the start.
        view.ShowTemplate(entry, Instance(entry, CalcInstanceStatus.Issued));
        view.SetReferenceOptions("Fasteners", Fasteners());
        Assert.True(view.IsReferencePickerSplit("Fasteners"));
        Assert.False(Picker(view, CalcSheetView.ReferenceSizePickerName("Fasteners")).IsEnabled);
        Assert.False(Picker(view, CalcSheetView.ReferenceGradePickerName("Fasteners")).IsEnabled);
    }

    [AvaloniaFact]
    public void Records_Without_Sizes_Keep_One_Picker()
    {
        var (view, _, picked) = BoltedJoint();

        // Labels alone (no size), and sizes with one record each, read best as one list.
        view.SetReferenceOptions("Fasteners", ["FX-BOLT-1 — FX-A", "FX-BOLT-2 — FX-B"]);
        Assert.False(view.IsReferencePickerSplit("Fasteners"));
        var single = Picker(view, CalcSheetView.ReferencePickerName("Fasteners"));
        Assert.True(single.IsVisible);
        Assert.Equal("Pick a released record (2)", single.PlaceholderText);

        view.SetReferenceOptions("Fasteners", [new CalcSheetView.ReferenceChoice("M3 x 0.5 - 8.8", "M3 x 0.5", "8.8"), new CalcSheetView.ReferenceChoice("M4 x 0.7 - 8.8", "M4 x 0.7", "8.8")]);
        Assert.False(view.IsReferencePickerSplit("Fasteners"));
        single.SelectedIndex = 1;
        Assert.Equal([1], picked);

        view.SetReferenceOptions("Fasteners", Array.Empty<CalcSheetView.ReferenceChoice>());
        Assert.Equal("No released record", single.PlaceholderText);
        Assert.False(view.IsReferencePickerEnabled("Fasteners"));
    }

    [AvaloniaFact]
    public void The_Split_Survives_A_Rebuilt_Form()
    {
        var (view, _, _) = BoltedJoint();
        view.SetReferenceOptions("Fasteners", Fasteners());
        view.ApplyImportedInputs(view.ReadForm());

        Assert.True(view.IsReferencePickerSplit("Fasteners"));
        Assert.Equal(5, Picker(view, CalcSheetView.ReferenceSizePickerName("Fasteners")).ItemCount);
    }

    // ------------------------------------------------------------------ comment 2: labels

    [Fact]
    public void Every_Input_Of_Every_Template_Shows_A_Short_Name_And_Keeps_Its_Full_Description_In_The_Tooltip()
    {
        var calculators = CalcCalculatorComposition.All();
        Assert.True(calculators.Count >= 54, $"{calculators.Count} templates");
        var problems = new List<string>();

        foreach (var template in calculators.Select(c => c.Template))
        {
            var names = CalcInputLabel.Names(template);

            foreach (var spec in template.Inputs)
            {
                var name = names[spec.Symbol];

                if (name.Length < 2 || name.Length > CalcInputLabel.MaxNameLength || name.Contains('(', StringComparison.Ordinal) || name.Contains(')', StringComparison.Ordinal))
                    problems.Add($"{template.Id} {spec.Symbol}: '{name}' ({name.Length})");

                if (!CalcInputLabel.Tooltip(spec).Contains(spec.Description, StringComparison.Ordinal))
                    problems.Add($"{template.Id} {spec.Symbol}: the tooltip does not hold the description");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Theory]
    [InlineData("Span", "Span")]
    [InlineData("Distributed load, downward (optional; blank = not used by the point-load and couple cases)", "Distributed load")]
    [InlineData("Bolt 0.2 % proof (yield) strength", "Bolt 0.2 % proof strength")]
    [InlineData("Nut factor (nut-factor method); F_M is the maximum preload", "Nut factor")]
    [InlineData("Temperature of use. Recorded: Es and the permissible strain", "Temperature of use")]
    [InlineData("Slenderness factor for cl. 6.3.2.3 of the code", "Slenderness factor for cl. 6.3.2.3 of the code")]
    [InlineData("Ratio √(a/b) of the parts", "Ratio √(a/b) of the parts")]
    [InlineData("Unclosed (aside", "Unclosed")]
    public void A_Name_Is_The_Description_Before_Its_First_Break_Without_Asides(string description, string name)
    {
        Assert.Equal(name, CalcInputLabel.Name(description));
    }

    [AvaloniaFact]
    public void The_Beam_Formulas_Labels_Read_Symbol_And_Name_With_The_Description_On_Hover()
    {
        var catalogue = new CalcCatalogue(CalcCalculatorComposition.All());
        var view = new CalcSheetView();
        view.ShowCatalogue(catalogue.Current());
        view.SelectTemplate(Tempest.Calc.Structural.BeamFormulasCalculator.Id);

        Assert.Equal("L — Span", view.InputLabelText("L"));
        Assert.Equal("w — Distributed load", view.InputLabelText("w"));
        Assert.Equal("F — Point load", view.InputLabelText("F"));
        Assert.Equal("M_0 — Applied couple", view.InputLabelText("M_0"));
        Assert.Equal("sigma_allow — Allowable bending stress", view.InputLabelText("sigma_allow"));
        Assert.Equal("y_c — Extreme-fibre distance", view.InputLabelText("y_c"));

        var w = view.SelectedTemplate!.Template.FindInput("w")!;
        Assert.Contains("downward (optional; blank = not used by the point-load and couple cases)", w.Description, StringComparison.Ordinal);
        Assert.Contains(w.Description, view.InputLabelTip("w"), StringComparison.Ordinal);
        Assert.Contains(w.Description, ToolTip.GetTip(view.FieldControl("w")!) as string, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ comment 4: the catalogue

    [AvaloniaFact]
    public void Every_Catalogue_Row_Shows_Its_Title_Only_And_Keeps_Version_And_Hash_On_Hover_And_On_The_Opened_Sheet()
    {
        var catalogue = new CalcCatalogue(CalcCalculatorComposition.All());
        var view = new CalcSheetView();
        view.ShowCatalogue(catalogue.Current());
        var tree = view.GetLogicalDescendants().OfType<TreeView>().Single(t => AutomationProperties.GetName(t) == CalcSheetView.CatalogueAutomationName);
        var leaves = tree.Items.OfType<TreeViewItem>().SelectMany(d => d.Items.OfType<TreeViewItem>()).Where(l => l.Tag is CalcCatalogueEntry).ToList();
        // 54 templates; the two lifting ones are shown only where the Lifting policy is on.
        Assert.True(leaves.Count >= 52, $"{leaves.Count} rows");

        foreach (var leaf in leaves)
        {
            var entry = (CalcCatalogueEntry)leaf.Tag!;
            Assert.Equal(entry.Template.Title, leaf.Header);
            var tip = ToolTip.GetTip(leaf) as string;
            Assert.Contains($"Version {entry.Template.Version}", tip, StringComparison.Ordinal);
            Assert.Contains(entry.ShortHash, tip, StringComparison.Ordinal);
        }

        var beam = catalogue.Get(Tempest.Calc.Structural.BeamFormulasCalculator.Id);
        view.SelectTemplate(beam.Reference);
        Assert.Contains(beam.Reference, view.TemplateReferenceText, StringComparison.Ordinal);
        Assert.Contains($"hash {beam.ShortHash}", view.TemplateReferenceText, StringComparison.Ordinal);
    }
}
