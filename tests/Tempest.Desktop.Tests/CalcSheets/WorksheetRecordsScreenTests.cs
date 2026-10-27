using System.Text;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Identity;
using Tempest.Desktop.CalcSheets;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>
/// Addendum C, C-3, through the real window: a worksheet says which required records it is missing and links to the records screen; the screen
/// takes a Track P template either typed into a grid or from a CSV (preview, column mapping, unit check, row errors, all-or-nothing commit, the
/// provenance required and stamped, licensed by default); and nothing on it exports a record's values. Every number in these files is invented.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class WorksheetRecordsScreenTests
{
    private const string Ampacity = "CSA[mm2],AWG[-],I_t[A]";

    private sealed record Shell(WorkspaceHost Host, MainWindow Window, EngineeringAreaView Area, CalcSheetView Sheet, CalcSheetCoordinator Coordinator);

    [AvaloniaFact]
    public async Task A_Worksheet_Lists_Its_Missing_Required_Records_Links_To_The_Screen_And_An_Imported_File_Clears_Them()
    {
        var shell = await OpenAsync();

        try
        {
            var (host, window, area, sheet, _) = shell;
            sheet.SelectTemplate("elec.conductor-sizing");
            await Settle();

            // The worksheet says what it lacks, in words, with a link per record type.
            Assert.True(sheet.IsRequirementsShown);
            Assert.Equal(["cable.ampacity", "device.ratings"], sheet.MissingRequiredKinds.Order(StringComparer.Ordinal));
            Assert.StartsWith("2 required records are missing", sheet.RequirementsSummary, StringComparison.Ordinal);
            Assert.Contains(sheet.GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? string.Empty).Contains("E4 cannot size a conductor", StringComparison.Ordinal));
            Assert.Contains(sheet.GetLogicalDescendants().OfType<Button>(), b => AutomationProperties.GetName(b) == "Fill record: Cable ampacity (current-carrying capacities)");

            // The link lands on the records screen, on that record type.
            Click(sheet, CalcSheetView.FillRecordName("Cable ampacity (current-carrying capacities)"));
            await Settle();
            var screen = window.GetLogicalDescendants().OfType<CalcRecordsView>().Single();
            Assert.Equal("cable.ampacity", screen.Model.SelectedKind);
            Assert.Equal("04 bs7671.ampacity (cable.ampacity)", screen.Model.Template!.Label);
            Assert.Contains(screen.GetLogicalDescendants().OfType<ListBoxItem>(), i => AutomationProperties.GetName(i) == "Record type: Cable ampacity (current-carrying capacities)");
            Assert.Equal("test.engineer", screen.GetLogicalDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == CalcRecordsView.EnteredByName).Text);

            // Provenance is required: without it the import is not offered.
            await screen.Model.LoadFileBytesAsync("ampacity.csv", Csv(Ampacity, "1.5,,15", "2.5,,20", "4,,27.5"));
            await Settle();
            Assert.All(screen.Model.FilePreview!.Errors, e => Assert.Contains("provenance needs", e.Message, StringComparison.Ordinal));
            Assert.False(screen.Model.CanImportFile);
            Assert.Contains("needs its source title", TextOf(screen, CalcRecordsView.ProvenanceProblemName), StringComparison.Ordinal);
            Assert.False(ButtonOf(screen, CalcRecordsView.ImportFileCaption).IsEnabled);

            await FillProvenanceAsync(screen, "Method C ampacity (test)");
            await Settle();
            Assert.Contains("Ready: 1 record", TextOf(screen, CalcRecordsView.FileSummaryName), StringComparison.Ordinal);
            Assert.True(ButtonOf(screen, CalcRecordsView.ImportFileCaption).IsEnabled);

            // The column mapping is on screen, one row per file column, each with its unit check.
            var mapCsa = screen.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == "Map column CSA");
            Assert.Equal("CSA", mapCsa.SelectedItem);
            Assert.Contains(screen.GetLogicalDescendants().OfType<TextBlock>(), t => AutomationProperties.GetName(t) == "Column check I_t" && (t.Text ?? string.Empty).Contains("ampacity.I_t", StringComparison.Ordinal));
            Assert.Contains("ampacity.csv", TextOf(screen, CalcRecordsView.FileNameLabel), StringComparison.Ordinal);

            Click(screen, CalcRecordsView.ImportFileCaption);
            await Settle();

            var saved = Assert.Single(await host.CalcInstances!.Store.ListRecordsAsync("cable.ampacity"));
            Assert.Equal("Method C ampacity (test)", saved.Title);
            Assert.Equal("Test Wiring Rules", saved.SourceTitle);
            Assert.Equal("2018; A2:2022", saved.Edition);
            Assert.Equal("Table 4D2A", saved.TableOrClause);
            Assert.Equal("test.engineer", saved.EnteredBy);
            Assert.True(saved.LicensedSource, "licensed_source is on by default");
            Assert.Equal(3, saved.Values.Single().Rows!.Count);
            Assert.NotNull(saved.Values.Single().Source!.EvidenceSha256);
            Assert.Contains("Saved 'Method C ampacity (test)' as a reference record (rev 1, licensed: its values never leave in an export)", TextOf(screen, CalcRecordsView.StatusName), StringComparison.Ordinal);

            // The list shows the record and that it is licensed, never its numbers.
            var listed = screen.GetLogicalDescendants().OfType<TextBlock>().Single(t => (AutomationProperties.GetName(t) ?? string.Empty).StartsWith("Record: Method C ampacity", StringComparison.Ordinal));
            Assert.Contains("LICENSED (never exported)", listed.Text, StringComparison.Ordinal);
            Assert.Contains("ampacity (3 rows)", listed.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("27.5", listed.Text, StringComparison.Ordinal);

            // What the screen saved is what the worksheet reads again, with no step in between: only the ratings are missing now.
            Assert.Equal(["device.ratings"], sheet.MissingRequiredKinds);
            Assert.StartsWith("1 required record is missing", sheet.RequirementsSummary, StringComparison.Ordinal);
            area.SelectNode("Worksheets");
            await Settle();
            Assert.Same(sheet, window.GetLogicalDescendants().OfType<CalcSheetView>().Single());
            Assert.Equal(["device.ratings"], sheet.MissingRequiredKinds);
        }
        finally
        {
            await CloseAsync(shell);
        }
    }

    [AvaloniaFact]
    public async Task A_Table_Typed_Into_The_Grid_Is_Checked_Live_With_Its_Units_And_Saved_All_Or_Nothing()
    {
        var shell = await OpenAsync();

        try
        {
            var (host, window, area, sheet, _) = shell;
            area.SelectNode("Reference records screen");
            await Settle();
            var screen = window.GetLogicalDescendants().OfType<CalcRecordsView>().Single();
            await screen.Model.SelectKindAsync("device.ratings");
            await Settle();
            Assert.Equal("47", screen.Model.Template!.Id);
            await FillProvenanceAsync(screen, "Preferred ratings (test)");

            Type(screen, "Row 1 I_n", "6");
            await Settle();
            Assert.Contains("Ready: 1 record", TextOf(screen, CalcRecordsView.GridSummaryName), StringComparison.Ordinal);
            Assert.Contains("unit ok", TextOf(screen, "Unit check of I_n"), StringComparison.Ordinal);

            // A unit of another kind is refused at once, in the header, and nothing can be saved.
            Type(screen, "Unit of I_n", "V");
            await Settle();
            Assert.Contains("WRONG unit", TextOf(screen, "Unit check of I_n"), StringComparison.Ordinal);
            Assert.False(ButtonOf(screen, CalcRecordsView.SaveGridCaption).IsEnabled);
            Assert.Contains(screen.Model.GridIssueLines, l => l.StartsWith("Error", StringComparison.Ordinal) && l.Contains("does not measure the same quantity", StringComparison.Ordinal));

            // A unit typed with a comma or a bracket cannot rewrite the header: it is refused in words and nothing can be saved.
            Type(screen, "Unit of I_n", "A,x");
            await Settle();
            Assert.Contains(screen.Model.GridIssueLines, l => l.Contains("comma, a quote, a bracket or a line break", StringComparison.Ordinal));
            Assert.False(ButtonOf(screen, CalcRecordsView.SaveGridCaption).IsEnabled);
            Assert.Null(await screen.Model.SaveGridAsync());
            Assert.Contains("Nothing was saved", screen.Model.Status, StringComparison.Ordinal);

            // A unit of the same kind is kept as typed and judged in the calculator's unit.
            Type(screen, "Unit of I_n", "mA");
            Type(screen, "Row 1 I_n", "6000");
            await Settle();
            Assert.Contains("kept as typed", TextOf(screen, "Unit check of I_n"), StringComparison.Ordinal);
            Assert.True(ButtonOf(screen, CalcRecordsView.SaveGridCaption).IsEnabled);
            Type(screen, "Unit of I_n", "A");
            Type(screen, "Row 1 I_n", "6");
            await Settle();

            // A second row that repeats the first is a row-level error on that row.
            Click(screen, CalcRecordsView.AddRowCaption);
            await Settle();
            Type(screen, "Row 2 I_n", "6");
            await Settle();
            Assert.Contains(screen.Model.GridIssueLines, l => l.StartsWith("Error · Row 2, I_n", StringComparison.Ordinal));
            Assert.False(ButtonOf(screen, CalcRecordsView.SaveGridCaption).IsEnabled);
            Assert.Empty(await host.CalcInstances!.Store.ListRecordsAsync("device.ratings"));

            Type(screen, "Row 2 I_n", "10");
            await Settle();
            Assert.True(ButtonOf(screen, CalcRecordsView.SaveGridCaption).IsEnabled, string.Join("; ", screen.Model.GridIssueLines));
            Click(screen, CalcRecordsView.SaveGridCaption);
            await Settle();

            var saved = Assert.Single(await host.CalcInstances.Store.ListRecordsAsync("device.ratings"));
            Assert.Equal(2, saved.Values.Single().Rows!.Count);
            Assert.True(saved.IsLicensed());
            Assert.Null(saved.Values.Single().Source!.EvidenceSha256);

            // The sheet can take it.
            var project = await host.ProjectDirectory!.CreateAsync("P-GRID", "Grid project");
            await host.ProjectContext!.OpenAsync(project.Id);
            var calc = await host.CalcInstances.CreateAsync("elec.conductor-sizing", project.Id, "Feeder");
            var applied = await host.CalcInstances.ApplyRecordAsync(calc.Id, saved.Id);
            Assert.True(applied.Succeeded, applied.Reason);
            Assert.Equal(2, applied.Instance!.Inputs.Single(i => i.Symbol == "ratings").Rows!.Count);
            Assert.NotNull(sheet);
        }
        finally
        {
            await CloseAsync(shell);
        }
    }

    [AvaloniaFact]
    public async Task A_File_With_A_Wrong_Unit_A_Bad_Row_Or_A_Broken_Structure_Is_Refused_Whole_And_Nothing_Is_Saved()
    {
        var shell = await OpenAsync();

        try
        {
            var (host, window, area, _, _) = shell;
            area.SelectNode("Reference records screen");
            await Settle();
            var screen = window.GetLogicalDescendants().OfType<CalcRecordsView>().Single();
            await screen.Model.SelectKindAsync("cable.correction-factors");
            await Settle();
            Assert.Equal(["06", "07", "08", "11"], screen.Model.Templates.Select(t => t.Id));
            await screen.Model.SelectTemplateAsync("06");
            await FillProvenanceAsync(screen, "Ambient factors (test)");
            var ambient = "medium[text],insulation[text],theta_a[degC],C_a[-]";

            // Unit mismatch: the column check says so and Import is not offered.
            await screen.Model.LoadFileBytesAsync("units.csv", Csv("medium[text],insulation[text],theta_a[A],C_a[-]", "air,PVC,20,0.99"));
            await Settle();
            Assert.False(screen.Model.CanImportFile);
            Assert.Contains(screen.Model.FileIssueLines, l => l.Contains("theta_a", StringComparison.Ordinal) && l.Contains("does not measure the same quantity", StringComparison.Ordinal));
            Assert.Contains("WRONG unit", TextOf(screen, "Column check theta_a"), StringComparison.Ordinal);
            Assert.False(ButtonOf(screen, CalcRecordsView.ImportFileCaption).IsEnabled);

            // One bad row among good ones: named by line, nothing is saved.
            await screen.Model.LoadFileBytesAsync("rows.csv", Csv(ambient, "air,PVC,20,0.99", "air,PVC,30,0.9", "air,PVC,40,oops"));
            await Settle();
            Assert.False(screen.Model.CanImportFile);
            Assert.Contains(screen.Model.FileIssueLines, l => l.StartsWith("Error · Line 4, C_a", StringComparison.Ordinal));
            Assert.Contains("Nothing will be saved", TextOf(screen, CalcRecordsView.FileSummaryName), StringComparison.Ordinal);
            var forced = await screen.Model.ImportFileAsync();
            Assert.False(forced!.Committed);
            Assert.Empty(await host.CalcInstances!.Store.ListRecordsAsync("cable.correction-factors"));

            // A malformed file is refused with the line.
            await screen.Model.LoadFileBytesAsync("quote.csv", Encoding.UTF8.GetBytes(ambient + "\r\nair,PVC,20,\"0.99\r\n"));
            await Settle();
            Assert.Contains("never closed", string.Join(" ", screen.Model.FileIssueLines), StringComparison.Ordinal);
            Assert.Contains("refused", TextOf(screen, CalcRecordsView.FileSummaryName), StringComparison.Ordinal);

            // A column the template does not have can be left out in the mapping, and then the file imports whole.
            await screen.Model.LoadFileBytesAsync("extra.csv", Csv(ambient + ",note[text]", "air,PVC,20,0.99,first", "air,PVC,30,0.9,second"));
            await Settle();
            Assert.False(screen.Model.CanImportFile);
            var mapNote = screen.GetLogicalDescendants().OfType<ComboBox>().SingleOrDefault(c => AutomationProperties.GetName(c) == "Map column note");
            Assert.NotNull(mapNote);
            mapNote!.SelectedItem = CalcRecordsView.LeaveOut;
            await Settle();
            Assert.True(screen.Model.CanImportFile, string.Join("; ", screen.Model.FileIssueLines));
            Click(screen, CalcRecordsView.ImportFileCaption);
            await Settle();
            Assert.Equal(2, (await host.CalcInstances.Store.ListRecordsAsync("cable.correction-factors")).Count);
            Assert.Contains("Saved 2 reference records", TextOf(screen, CalcRecordsView.StatusName), StringComparison.Ordinal);
        }
        finally
        {
            await CloseAsync(shell);
        }
    }

    [AvaloniaFact]
    public async Task The_Number_Format_Is_Chosen_On_The_Screen_And_The_Provenance_Can_Be_Read_From_The_Track_P_File()
    {
        var shell = await OpenAsync();

        try
        {
            var (host, window, area, _, _) = shell;
            area.SelectNode("Reference records screen");
            await Settle();
            var screen = window.GetLogicalDescendants().OfType<CalcRecordsView>().Single();
            await screen.Model.SelectKindAsync("cable.ampacity");

            var provenance = "kind,title,source_title,edition,amendment,table_or_clause,licence_holder,entered_by,licensed_source,origin,notes\r\n"
                + "cable.ampacity,Method B ampacity (test),Test Wiring Rules,2018,A2:2022,Table 4D5A,Own copy (test),someone else,,UserEntered,Reference 30 degC\r\n";
            screen.Model.LoadProvenanceFile(Encoding.UTF8.GetBytes(provenance));
            await Settle();
            Assert.Equal("Method B ampacity (test)", screen.GetLogicalDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == CalcRecordsView.TitleFieldName).Text);
            Assert.True(screen.GetLogicalDescendants().OfType<CheckBox>().Single(c => AutomationProperties.GetName(c) == CalcRecordsView.LicensedName).IsChecked);
            Assert.Null(screen.Model.ProvenanceProblem);

            // Under a decimal point the comma file is an error that names the format; chosen on the screen it reads.
            await screen.Model.LoadFileBytesAsync("comma.csv", Encoding.UTF8.GetBytes("CSA[mm2];AWG[-];I_t[A]\r\n2,5;;20,5\r\n"));
            await Settle();
            Assert.False(screen.Model.CanImportFile);
            Assert.Contains(screen.Model.FileIssueLines, l => l.Contains("decimal mark", StringComparison.Ordinal));
            screen.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == CalcRecordsView.NumberFormatName).SelectedIndex = 1;
            await Settle();
            Assert.True(screen.Model.CanImportFile, string.Join("; ", screen.Model.FileIssueLines));
            Click(screen, CalcRecordsView.ImportFileCaption);
            await Settle();
            var saved = Assert.Single(await host.CalcInstances!.Store.ListRecordsAsync("cable.ampacity"));
            Assert.Equal(20.5, saved.Values.Single().Rows![0].Cells["I_t"].Number);
            Assert.Equal("test.engineer", saved.EnteredBy);
        }
        finally
        {
            await CloseAsync(shell);
        }
    }

    [AvaloniaFact]
    public async Task The_Export_Lock_Still_Holds_The_Screen_Has_No_Export_And_A_Licensed_Import_Is_Withheld_From_An_Export()
    {
        var shell = await OpenAsync();

        try
        {
            var (host, window, area, _, _) = shell;
            area.SelectNode("Reference records screen");
            await Settle();
            var screen = window.GetLogicalDescendants().OfType<CalcRecordsView>().Single();
            await screen.Model.SelectKindAsync("cable.ampacity");
            await FillProvenanceAsync(screen, "Locked ampacity (test)");
            await screen.Model.LoadFileBytesAsync("locked.csv", Csv(Ampacity, "1.5,,15", "2.5,,20"));
            await Settle();
            Click(screen, CalcRecordsView.ImportFileCaption);
            await Settle();
            var record = Assert.Single(await host.CalcInstances!.Store.ListRecordsAsync("cable.ampacity"));

            // No control on the screen writes values out.
            Assert.DoesNotContain(screen.GetLogicalDescendants().OfType<Button>().Where(b => b is not Avalonia.Controls.Primitives.ToggleButton), b => (AutomationProperties.GetName(b) ?? string.Empty).Contains("xport", StringComparison.Ordinal) || (b.Content?.ToString() ?? string.Empty).Contains("xport", StringComparison.Ordinal));

            // The service's lock is as it was: the record is refused for export, and a calculation that applied it is exported without the rows.
            var (csv, refusal) = await host.CalcInstances.ExportRecordAsync(record.Id);
            Assert.Null(csv);
            Assert.Contains("never exported", refusal, StringComparison.Ordinal);

            var project = await host.ProjectDirectory!.CreateAsync("P-LOCK", "Lock project");
            await host.ProjectContext!.OpenAsync(project.Id);
            var calc = await host.CalcInstances.CreateAsync("elec.conductor-sizing", project.Id, "Feeder");
            var applied = await host.CalcInstances.ApplyRecordAsync(calc.Id, record.Id);
            Assert.True(applied.Succeeded, applied.Reason);
            var withheld = await host.CalcInstances.WithholdLicensedAsync(new CalcInputSet(applied.Instance!.Inputs));
            Assert.Null(withheld.Find("ampacity")!.Rows);
            Assert.Contains("licensed value withheld", withheld.Find("ampacity")!.Provenance!.Reference, StringComparison.Ordinal);

            // An unlicensed import (the person unticks the box) is a record that exports as before.
            await screen.Model.SelectKindAsync("cable.ampacity");
            await FillProvenanceAsync(screen, "Open ampacity (test)");
            screen.GetLogicalDescendants().OfType<CheckBox>().Single(c => AutomationProperties.GetName(c) == CalcRecordsView.LicensedName).IsChecked = false;
            await Settle();
            Assert.False(screen.Model.Licensed);
            await screen.Model.LoadFileBytesAsync("open.csv", Csv(Ampacity, "1.5,,15"));
            await Settle();
            Click(screen, CalcRecordsView.ImportFileCaption);
            await Settle();
            var open = (await host.CalcInstances.Store.ListRecordsAsync("cable.ampacity")).Single(r => r.Title == "Open ampacity (test)");
            Assert.False(open.IsLicensed());
            Assert.NotNull((await host.CalcInstances.ExportRecordAsync(open.Id)).Csv);
        }
        finally
        {
            await CloseAsync(shell);
        }
    }

    [AvaloniaFact]
    public async Task The_Reference_Records_Panel_Of_A_Saved_Draft_Opens_The_Records_Screen()
    {
        var shell = await OpenAsync();

        try
        {
            var (host, window, area, sheet, coordinator) = shell;
            var project = await host.ProjectDirectory!.CreateAsync("P-OPEN", "Open project");
            await host.ProjectContext!.OpenAsync(project.Id);
            var calc = await host.CalcInstances!.CreateAsync("elec.conductor-sizing", project.Id, "Feeder");
            area.SelectNode("Worksheets");
            await Settle();
            await coordinator.OpenAsync(calc.Id);
            await Settle();
            Assert.True(sheet.IsRecordPanelShown);

            Click(sheet, CalcSheetView.OpenRecordsCaption);
            await Settle();

            var screen = window.GetLogicalDescendants().OfType<CalcRecordsView>().Single();
            Assert.Equal("cable.ampacity", screen.Model.SelectedKind);
            Assert.Same(screen, window.GetLogicalDescendants().OfType<CalcRecordsView>().Single());
            Assert.Empty(window.GetLogicalDescendants().OfType<CalcSheetView>());
        }
        finally
        {
            await CloseAsync(shell);
        }
    }

    // ------------------------------------------------------------------

    private static async Task<Shell> OpenAsync()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        await host.StartAsync();
        var window = new MainWindow(host, new StubFilePicker());
        window.Show();
        host.SwitchPrincipal(new SessionPrincipal("test.engineer", "Test Engineer", SessionRole.Engineer));
        var project = await host.ProjectDirectory!.CreateAsync("P-REC-SCREEN", "Records screen project");
        await host.ProjectContext!.OpenAsync(project.Id);
        await host.ShellNavigator!.GoToModuleAsync(ShellArea.EngineeringDepartment);
        await window.RenderCurrentModuleAsync();
        await Settle();

        var area = window.GetLogicalDescendants().OfType<EngineeringAreaView>().Single();
        area.SelectNode("Worksheets");
        await Settle();
        var sheet = window.GetLogicalDescendants().OfType<CalcSheetView>().Single();
        var coordinator = (CalcSheetCoordinator)typeof(MainWindow).GetField("_calcSheetCoordinator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(window)!;
        return new Shell(host, window, area, sheet, coordinator);
    }

    private static async Task CloseAsync(Shell shell)
    {
        await shell.Host.ShutdownAsync();
        await shell.Host.DisposeAsync();
    }

    private static byte[] Csv(string header, params string[] lines) => Encoding.UTF8.GetBytes(header + "\r\n" + string.Join("\r\n", lines) + "\r\n");

    /// <summary>Types the provenance and lets the typing reach the model (a text box raises its change a moment after it is set).</summary>
    private static async Task FillProvenanceAsync(CalcRecordsView screen, string title)
    {
        Type(screen, CalcRecordsView.TitleFieldName, title);
        Type(screen, CalcRecordsView.SourceFieldName, "Test Wiring Rules");
        Type(screen, CalcRecordsView.EditionFieldName, "2018");
        Type(screen, CalcRecordsView.AmendmentFieldName, "A2:2022");
        Type(screen, CalcRecordsView.TableFieldName, "Table 4D2A");
        Type(screen, CalcRecordsView.HolderFieldName, "Own copy (test)");
        await Settle();
    }

    private static void Type(Control root, string name, string text) =>
        root.GetLogicalDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == name).Text = text;

    private static string TextOf(Control root, string name) =>
        root.GetLogicalDescendants().OfType<TextBlock>().Single(t => AutomationProperties.GetName(t) == name).Text ?? string.Empty;

    private static Button ButtonOf(Control root, string name) =>
        root.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == name);

    private static void Click(Control root, string name)
    {
        var button = ButtonOf(root, name);
        Assert.True(button.IsEnabled && button.IsEffectivelyVisible, $"'{name}' is not available.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static async Task Settle()
    {
        for (var i = 0; i < 24; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(25);
        }
    }
}
