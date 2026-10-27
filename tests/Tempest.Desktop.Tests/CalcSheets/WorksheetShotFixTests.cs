using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
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
    [AvaloniaFact]
    public void Item5_A_Long_Text_Takes_A_Full_Width_Row_And_A_Swept_Vector_Is_One_Summary_Line()
    {
        var (view, window) = Shown(1400);
        var notice = "This is a design calculation, not a thorough examination of the lifting accessory; the examination remains the competent person's duty under the regulations in force.";
        var angles = Enumerable.Range(0, 73).Select(i => i * 5.0).ToList();
        var outputs = new List<CalcOutput>
        {
            new("sheet_notice", "What this sheet is", new Tempest.Calc.Engine.Values.TextValue(notice)),
            new("theta2", "Crank angle", Tempest.Calc.Engine.Values.VectorValue.Of(angles, "deg")),
            new("short", "A short text", new Tempest.Calc.Engine.Values.TextValue("ok")),
        };
        view.ShowRun(new CalcRunResult([], outputs, [], [], [], []));
        window.UpdateLayout();

        // What the form reports as shown is still the sheet's own text.
        Assert.Equal(outputs.Select(o => (o.Symbol, o.ValueText)), view.ShownResults);

        var texts = view.GetLogicalDescendants().OfType<TextBlock>().ToList();
        var wide = texts.Single(t => AutomationProperties.GetName(t) == "Result sheet_notice");
        Assert.True(wide.Bounds.Width > 400, $"the long text is {wide.Bounds.Width} px wide");

        var summary = texts.Single(t => AutomationProperties.GetName(t) == "Result theta2");
        Assert.StartsWith("73 values, 0 to 360", SymbolText.Raw(summary), StringComparison.Ordinal);
        var all = view.GetLogicalDescendants().OfType<Expander>().Single(e => AutomationProperties.GetName(e) == "Result theta2 all values");
        Assert.False(all.IsExpanded);
        Assert.Contains("355, 360", ((SelectableTextBlock)all.Content!).Text, StringComparison.Ordinal);

        var small = texts.Single(t => AutomationProperties.GetName(t) == "Result short");
        Assert.Equal("\"ok\"", SymbolText.Raw(small));
    }
    private static CalcSheetView Live(string templateId)
    {
        var catalogue = new CalcCatalogue(CalcCalculatorComposition.All(), new CalcFeatureFlags(CalcFeatures.All));
        var view = new CalcSheetView();
        view.InputsChanged += inputs => view.ShowRun(CalcRunner.Run(catalogue.Get(templateId).Calculator, inputs));
        view.ShowCatalogue(catalogue.Current());
        view.SelectTemplate(templateId);
        return view;
    }

    private static RecordRequirement Required(string kind, params string[] symbols) => new(kind, kind, symbols, RecordNeed.Required, "needed", Present: false, Count: 0, []);

    [AvaloniaFact]
    public void Item6_A_Required_Record_Is_Missing_Only_While_Nothing_On_The_Form_Gives_It()
    {
        var view = Live("mech.snap-fit");
        view.SetRecordRequirements(view.SelectedTemplate!.Reference, [Required("snapfit.friction", "mu")]);

        // On the defaults mu is empty: genuinely missing.
        Assert.Equal(["snapfit.friction"], view.MissingRequiredKinds);
        Assert.StartsWith("1 required record is missing", view.RequirementsSummary, StringComparison.Ordinal);

        // Typed by hand: not missing, but its source is still asked for.
        view.SetField("mu", text: "0.35");
        Assert.Empty(view.MissingRequiredKinds);
        Assert.StartsWith("1 required value is typed on the form without a source", view.RequirementsSummary, StringComparison.Ordinal);

        // Typed with a source: met.
        view.SetField("mu", provenance: "spec", reference: "Maker's data sheet, table 4");
        Assert.Empty(view.MissingRequiredKinds);
        Assert.StartsWith("All 1 required record(s) are entered, or given on the form", view.RequirementsSummary, StringComparison.Ordinal);
        Assert.True(view.IsRequirementsShown);
    }

    [AvaloniaFact]
    public void Item6_The_Built_In_Factor_Set_Stands_In_For_The_Factors_Record()
    {
        var view = Live("struct.load-combinations");
        view.SetRecordRequirements(view.SelectedTemplate!.Reference, [Required("en1990.factors", "gamma_G_sup", "gamma_G_inf", "gamma_Q", "xi", "psi")]);

        view.SetField("factor_set", choice: "EN recommended");
        Assert.Empty(view.MissingRequiredKinds);
        Assert.DoesNotContain("missing", view.RequirementsSummary, StringComparison.Ordinal);

        // A National Annex set with the factors left as the sheet opened them: nothing was given, so the record is missing (review item 2).
        view.SetField("factor_set", choice: "National Annex record");
        Assert.Equal(["en1990.factors"], view.MissingRequiredKinds);
        Assert.StartsWith("1 required record is missing", view.RequirementsSummary, StringComparison.Ordinal);

        // A factor typed by hand is given, but has no source yet.
        view.SetField("gamma_Q", text: "1.6");
        Assert.Empty(view.MissingRequiredKinds);
        Assert.StartsWith("1 required value is typed on the form without a source", view.RequirementsSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void Item6_Every_Built_In_Alternative_Names_A_Real_Choice_Of_Its_Template()
    {
        var all = CalcCalculatorComposition.All().ToDictionary(c => c.Template.Id, c => c.Template, StringComparer.Ordinal);

        foreach (var (template, kind, symbol, choice) in RecordRequirements.BuiltInEntries)
        {
            Assert.True(all.TryGetValue(template, out var t), template);
            Assert.Contains(t!.RecordKinds ?? [], k => k.Kind == kind);
            Assert.Contains(choice, t.FindInput(symbol)!.Choices ?? []);
        }
    }

    [AvaloniaFact]
    public void Item6_An_Issued_Calculation_Shows_No_Missing_Records_Banner()
    {
        var view = Live("mech.snap-fit");
        var entry = view.SelectedTemplate!;
        var draft = Instance(entry, CalcInstanceStatus.Draft);
        view.ShowTemplate(entry, draft);
        view.SetRecordRequirements(entry.Reference, [Required("snapfit.friction", "mu")]);
        Assert.True(view.IsRequirementsShown);

        view.ShowInstance(draft with { Status = CalcInstanceStatus.Issued, IssuedAt = DateTimeOffset.UnixEpoch });
        Assert.False(view.IsRequirementsShown);

        view.ShowTemplate(entry, Instance(entry, CalcInstanceStatus.Issued));
        view.SetRecordRequirements(entry.Reference, [Required("snapfit.friction", "mu")]);
        Assert.False(view.IsRequirementsShown);
    }
    [AvaloniaFact]
    public void Item8_A_Long_Catalogue_Title_Wraps_And_Keeps_Its_Version_In_The_Column()
    {
        var long1 = new TestWorksheetCalculator();
        var longTitle = new LongTitle(long1);
        var catalogue = new CalcCatalogue([longTitle]);
        var view = new CalcSheetView();
        view.ShowCatalogue(catalogue.Current());
        var window = new Window { Width = 1200, Height = 900, Content = view };
        window.Show();
        window.UpdateLayout();

        var tree = view.GetLogicalDescendants().OfType<TreeView>().Single(t => AutomationProperties.GetName(t) == CalcSheetView.CatalogueAutomationName);
        var leaf = tree.Items.OfType<TreeViewItem>().Single().Items.OfType<TreeViewItem>().Single();
        Assert.Contains("v1.0.0", leaf.Header?.ToString(), StringComparison.Ordinal);

        var version = leaf.GetVisualDescendants().OfType<TextBlock>().Single(t => AutomationProperties.GetName(t) == CalcSheetView.LeafVersionName);
        Assert.StartsWith("v1.0.0 ", version.Text, StringComparison.Ordinal);
        var right = version.TranslatePoint(new Point(version.Bounds.Width, 0), view)!.Value.X;
        Assert.True(right <= 380, $"the version ends at {right} px, past the 380 px catalogue column");
    }

    private sealed class LongTitle(TestWorksheetCalculator inner) : ICalcCalculator
    {
        public CalcTemplate Template { get; } = inner.Template with { Title = "Glaser condensation (interstitial and surface) with a much longer title than fits" };

        public void Run(CalcRunContext context) => inner.Run(context);
    }

    [AvaloniaFact]
    public void Item9_The_Sign_Off_Section_Is_Shown_Only_When_It_Has_Something_In_It()
    {
        var (view, _) = Shown();
        Assert.False(view.IsGovernanceSectionVisible);
        Assert.DoesNotContain(view.GetLogicalDescendants().OfType<Expander>(), e => e.IsVisible && Equals(e.Header, CalcSheetView.GovernancePanelName));

        var lifting = Live("mech.lifting-lug");
        Assert.Equal("mech.lifting-lug", lifting.SelectedTemplate?.Template.Id);
        Assert.True(lifting.IsGovernancePanelVisible);
        Assert.True(lifting.IsGovernanceSectionVisible);
    }
    [AvaloniaFact]
    public void Item10_Repeated_Warnings_Of_One_Kind_Are_One_Collapsed_Notice_With_A_Count()
    {
        var (view, _) = Shown();
        var symbols = Enumerable.Range(1, 14).Select(i => $"k_{i}").ToList();
        var notices = symbols.Select(s => new CalcNotice("record." + s, CalcNoticeSeverity.Warning, $"{s}: a tabulated value not taken from a reference record (Lifting factors); enter it as one.")).ToList();
        notices.Add(new CalcNotice("high-stress", CalcNoticeSeverity.Warning, "u: the stress is above 80 % of the allowable."));
        view.ShowRun(new CalcRunResult([], [], [], notices, [], []));

        var group = Assert.Single(view.GetLogicalDescendants().OfType<Expander>(), e => AutomationProperties.GetName(e) == CalcSheetView.WarningGroupName);
        Assert.False(group.IsExpanded);
        Assert.StartsWith("Warning — 14 inputs hold a tabulated value not taken from a reference record (k_1, k_2", SymbolText.Raw((TextBlock)group.Header!), StringComparison.Ordinal);
        Assert.Equal(14, ((StackPanel)group.Content!).Children.Count);

        // A warning that stands alone is still a line of its own.
        Assert.Contains(view.GetLogicalDescendants().OfType<TextBlock>(), t => SymbolText.Raw(t) == "Warning — u: the stress is above 80 % of the allowable.");

        // Two of a kind stay as two lines.
        view.ShowRun(new CalcRunResult([], [], [], notices.Take(2).ToList(), [], []));
        Assert.DoesNotContain(view.GetLogicalDescendants().OfType<Expander>(), e => AutomationProperties.GetName(e) == CalcSheetView.WarningGroupName);
    }

    [AvaloniaFact]
    public void Item11_A_Long_Choice_Is_Shown_Whole_In_Its_Picker()
    {
        var view = Live("fp.oring-gland");
        var window = new Window { Width = 1600, Height = 1000, Content = view };
        window.Show();
        var spec = view.SelectedTemplate!.Template.FindInput("face_p")!;
        var longest = spec.Choices!.MaxBy(c => c.Length)!;
        view.SetField("face_p", choice: longest);
        window.UpdateLayout();

        var picker = (ComboBox)view.FieldControl("face_p")!;
        var shown = picker.GetVisualDescendants().OfType<TextBlock>().Single(t => AutomationProperties.GetName(t) == CalcSheetView.ChoiceTextName);
        Assert.Equal(longest, shown.Text);
        Assert.Equal(TextWrapping.Wrap, shown.TextWrapping);
        Assert.True(shown.Bounds.Width <= picker.Bounds.Width, $"the choice is {shown.Bounds.Width} px in a {picker.Bounds.Width} px picker");
        Assert.Equal(longest, view.ReadForm().Find("face_p")!.Text);
    }
}
