using System.Text;
using Tempest.Calc.Library;
using Tempest.Calc.Library.Dashboard;
using Tempest.Calc.Sheets;
using Tempest.Desktop.CalcSheets;
using Tempest.Desktop.Tests.Quotations;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>Addendum C, C-4: the view model behind Calculations → Verification, headless: filters, markdown and PDF export through the file picker.</summary>
public sealed class VerificationDashboardViewModelTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "tempest-verification-" + Guid.NewGuid().ToString("N"));
    private readonly StubFilePicker _picker = new();

    public VerificationDashboardViewModelTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, true);
        }
        catch (IOException)
        {
        }
    }

    private static CalcCatalogue Catalogue() => new(CalcCalculatorComposition.All());

    private VerificationDashboardViewModel Model(IRequiredRecordsSource? records = null, VerificationDashboardService? service = null) =>
        new(service ?? new VerificationDashboardService(Catalogue(), CalcPackTemplateComposition.All(), records: records), _picker, new VerificationPdfRenderer(), () => "TempestOS 0.26.0 test");

    private string Path_(string name) => Path.Combine(_folder, name);

    [Fact]
    public async Task Refresh_Lists_Every_Calculator_And_Chain_With_The_Discipline_Choices()
    {
        var model = Model();
        await model.RefreshAsync();
        var catalogue = Catalogue();

        Assert.Equal(catalogue.Current().Count + CalcPackTemplateComposition.All().Count, model.Rows.Count);
        Assert.Equal("All disciplines", model.DisciplineChoices[0]);
        Assert.Equal(["All disciplines", "Mechanical", "Structural", "Electrical", "Building services", "Fluid power", "Chains"], model.DisciplineChoices.Distinct());
        Assert.StartsWith($"{model.Rows.Count} of {model.Rows.Count} rows:", model.Summary, StringComparison.Ordinal);
        Assert.Equal(string.Empty, model.Status);
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task Filter_By_Discipline_And_Status_Narrows_The_Rows_And_Says_So_In_Words()
    {
        var model = Model();
        var changes = 0;
        model.Changed += () => changes++;
        await model.RefreshAsync();
        var all = model.Rows.Count;

        model.SetDiscipline("Fluid power");
        Assert.Equal(3, model.Rows.Count);
        Assert.All(model.Rows, r => Assert.Equal("Fluid power", r.Discipline));
        Assert.Equal("Discipline: Fluid power; status: all statuses", model.FilterText);

        model.SetDiscipline("Chains");
        Assert.Equal(CalcPackTemplateComposition.All().Count, model.Rows.Count);
        Assert.All(model.Rows, r => Assert.Equal(VerificationRowKind.Chain, r.Kind));

        model.SetDiscipline("All disciplines");
        model.SetStatus(VerificationStatusFilter.OpenDefects);
        Assert.InRange(model.Rows.Count, 1, all - 1);
        Assert.All(model.Rows, r => Assert.True(r.OpenDefectCount > 0));
        Assert.Contains("open defect(s)", model.Summary, StringComparison.Ordinal);

        model.SetDiscipline("Mechanical");
        Assert.All(model.Rows, r => Assert.True(r.Discipline == "Mechanical" && r.OpenDefectCount > 0));
        Assert.Equal("Discipline: Mechanical; status: open defects", model.FilterText);

        model.SetStatus(VerificationStatusFilter.Failing);
        Assert.Empty(model.Rows);
        Assert.StartsWith($"0 of {all} rows:", model.Summary, StringComparison.Ordinal);

        model.SetDiscipline("Not a discipline");
        Assert.Equal("All disciplines", model.DisciplineFilter);
        model.SetStatus((VerificationStatusFilter)999);
        Assert.Equal(VerificationStatusFilter.All, model.StatusFilter);
        Assert.Equal(all, model.Rows.Count);
        Assert.True(changes >= 6);
    }

    [Fact]
    public async Task Markdown_Export_Writes_The_Rows_Shown_To_The_Chosen_File()
    {
        var model = Model();
        await model.RefreshAsync();
        model.SetDiscipline("Fluid power");
        var path = Path_("dashboard.md");
        _picker.SetNextSavePath(path);

        await model.ExportMarkdownAsync();

        var text = File.ReadAllText(path);
        Assert.Contains("# TempestOS verification dashboard", text, StringComparison.Ordinal);
        Assert.Contains("TempestOS 0.26.0 test", text, StringComparison.Ordinal);
        Assert.Contains("Filter: Discipline: Fluid power; status: all statuses.", text, StringComparison.Ordinal);
        Assert.Contains("`fp.accumulator`", text, StringComparison.Ordinal);
        Assert.DoesNotContain("`mech.shaft`", text, StringComparison.Ordinal);
        Assert.Equal("Saved 3 row(s) of the verification dashboard to dashboard.md.", model.Status);

        var request = Assert.Single(_picker.SaveRequests);
        Assert.Matches(@"^tempestos-verification-\d{4}-\d{2}-\d{2}\.md$", request.SuggestedFileName);
        Assert.Equal("Export the verification dashboard as markdown", request.Title);
    }

    [Fact]
    public async Task Pdf_Export_Writes_A_Non_Empty_Pdf_That_Reads_As_Text()
    {
        var model = Model();
        await model.RefreshAsync();
        var path = Path_("dashboard.pdf");
        _picker.SetNextSavePath(path);

        await model.ExportPdfAsync();

        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 5_000, $"the PDF is {bytes.Length} bytes");
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        var text = PdfTextExtractor.ExtractText(bytes);
        Assert.Contains("VERIFICATION DASHBOARD", text, StringComparison.Ordinal);
        Assert.Contains("(mech.shaft)", text, StringComparison.Ordinal);
        Assert.Contains("Open defects", text, StringComparison.Ordinal);
        Assert.Equal($"Saved {model.Rows.Count} row(s) of the verification dashboard to dashboard.pdf.", model.Status);
    }

    [Fact]
    public async Task A_Path_Without_An_Extension_Gets_One_And_A_Cancelled_Pick_Saves_Nothing()
    {
        var model = Model();
        await model.RefreshAsync();
        _picker.SetNextSavePath(Path_("records"));
        await model.ExportMarkdownAsync();
        Assert.True(File.Exists(Path_("records.md")));

        _picker.SetNextSavePath(null);
        await model.ExportPdfAsync();
        Assert.Equal("The PDF export was cancelled; nothing was saved.", model.Status);
        Assert.Equal(["records.md"], Directory.GetFiles(_folder).Select(Path.GetFileName));
    }

    [Fact]
    public async Task With_No_Rows_Shown_An_Export_Is_Refused_Before_The_Picker_Opens()
    {
        var model = Model();
        await model.RefreshAsync();
        model.SetStatus(VerificationStatusFilter.Failing);

        await model.ExportMarkdownAsync();
        await model.ExportPdfAsync();

        Assert.Equal("There are no rows to export. Change the filter so that at least one row is shown.", model.Status);
        Assert.Empty(_picker.SaveRequests);
    }

    [Fact]
    public async Task An_Export_Before_The_Dashboard_Is_Opened_Is_Refused_The_Same_Way()
    {
        var model = Model();
        await model.ExportPdfAsync();

        Assert.Equal("There are no rows to export. Change the filter so that at least one row is shown.", model.Status);
        Assert.Empty(_picker.SaveRequests);
        Assert.Equal("Opening the verification dashboard…", model.Summary);
    }

    [Fact]
    public async Task A_Folder_That_Cannot_Be_Written_Gives_A_Plain_Message_Naming_The_Problem()
    {
        var model = Model();
        await model.RefreshAsync();
        _picker.SetNextSavePath(Path.Combine(_folder, "missing-folder", "dashboard.md"));

        await model.ExportMarkdownAsync();

        Assert.StartsWith("The markdown file could not be saved (", model.Status, StringComparison.Ordinal);
        Assert.EndsWith("Choose another folder or file name and try again.", model.Status, StringComparison.Ordinal);
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task A_Records_Lookup_That_Fails_Leaves_An_Empty_Dashboard_And_A_Sentence_Not_An_Exception()
    {
        var model = Model(records: new ThrowingRecords());

        await model.RefreshAsync();

        Assert.Empty(model.Rows);
        Assert.StartsWith("The verification dashboard could not be built, so it is empty.", model.Status, StringComparison.Ordinal);
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task The_Pdf_Draws_At_Most_The_Row_Cap_And_Says_So()
    {
        var one = (await new VerificationDashboardService(Catalogue(), [], records: null).BuildAsync()).Rows[0];
        var rows = Enumerable.Range(0, VerificationDashboardService.MaxRows + 200).Select(i => one with { Id = $"t.{i}", Title = new string('x', 5000) }).ToList();
        var report = new VerificationReport(rows.Take(VerificationDashboardService.MaxRows).ToList(), rows.Count, true, null, DateTimeOffset.UnixEpoch);

        var bytes = new VerificationPdfRenderer().Render(report, rows, "all", "test").ToArray();
        var text = PdfTextExtractor.ExtractText(bytes);

        Assert.True(bytes.Length > 5_000);
        Assert.Contains("Only the first 500 rows are listed.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("t.500)", text, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', VerificationPdfRenderer.MaxCellLength + 10), text, StringComparison.Ordinal);
    }

    private sealed class ThrowingRecords : IRequiredRecordsSource
    {
        public Task<IReadOnlyDictionary<string, IReadOnlyList<RecordRequirement>>> RequirementsAsync(IReadOnlyList<CalcTemplate> templates, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("the store is not open");
    }
}
