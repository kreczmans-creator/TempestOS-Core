using System.Runtime.Versioning;
using System.Text;
using Avalonia.Headless.XUnit;
using PDFtoImage;
using Tempest.Desktop.Documents.Timesheets;
using Tempest.Desktop.Tests.Quotations;

namespace Tempest.Desktop.Tests.Documents;

/// <summary>The weekly timesheet document renderer (`WP 21.2A`, scope item 2).</summary>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class TimesheetDocumentRendererTests
{
    [Fact]
    public void DocumentType_And_TemplateName_MatchTheDesignSystemMapping()
    {
        var renderer = new TimesheetDocumentRenderer();
        Assert.Equal("TIMESHEET", renderer.DocumentType);
        Assert.Equal("timesheet", renderer.TemplateName);
    }

    [AvaloniaFact]
    public void Render_ProducesAValidOnePagePdf()
    {
        var bytes = new TimesheetDocumentRenderer().Render(TimesheetDocumentModelFixtures.Minimal()).ToArray();

        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes, 0, Math.Min(8, bytes.Length)), StringComparison.Ordinal);
        Assert.Equal(1, Conversion.GetPageCount(bytes));
    }

    [AvaloniaFact]
    public void Render_EveryFieldTheTimesheetNames_IsReadableBackOutOfTheText()
    {
        var model = TimesheetDocumentModelFixtures.Minimal();
        var bytes = new TimesheetDocumentRenderer().Render(model).ToArray();
        var text = PdfTextExtractor.ExtractText(bytes);

        Assert.Contains("TIMESHEET", text, StringComparison.Ordinal);
        Assert.Contains(model.PrincipalName, text, StringComparison.Ordinal);
        Assert.Contains(model.WeekTotalHours, text, StringComparison.Ordinal);
        Assert.Contains(model.BillableHours, text, StringComparison.Ordinal);

        foreach (var row in model.Rows)
        {
            Assert.Contains(row.ProjectName, text, StringComparison.Ordinal);
            Assert.Contains(row.TaskDescription, text, StringComparison.Ordinal);
        }

        Assert.Contains("Prepared by", text, StringComparison.Ordinal);
        Assert.Contains("Approved by", text, StringComparison.Ordinal);
        Assert.Contains("Page 1 of 1", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Render_NoTimeRecorded_SaysSoRatherThanAnEmptyTable()
    {
        var model = TimesheetDocumentModelFixtures.Minimal() with { Rows = [] };
        var bytes = new TimesheetDocumentRenderer().Render(model).ToArray();
        var text = PdfTextExtractor.ExtractText(bytes);

        Assert.Contains("No time recorded this week", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Render_NullModel_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new TimesheetDocumentRenderer().Render(null!));
}

/// <summary>Fixture <see cref="TimesheetDocumentModel"/>s for the renderer tests.</summary>
internal static class TimesheetDocumentModelFixtures
{
    public static TimesheetDocumentModel Minimal() => new(
        PrincipalName: "priya.patel",
        WeekStart: new DateOnly(2026, 3, 2),
        Rows:
        [
            // Short project names deliberately — a long one word-wraps
            // across more than one table line (`DocumentTemplate.AddTable`'s
            // own narrow Project column), which loses the space at the
            // wrap point and would break this fixture's own "the whole
            // string is one readable substring" assertions; pagination and
            // wrapping themselves are `DocumentTemplateTests`' own concern.
            new TimesheetDocumentRow(new DateOnly(2026, 3, 2), "PRJ-100", "Concept design", 7.5m, true),
            new TimesheetDocumentRow(new DateOnly(2026, 3, 3), "PRJ-100", "Detailed calculation pack", 6m, true),
            new TimesheetDocumentRow(new DateOnly(2026, 3, 4), "Internal", "Team training", 2m, false),
        ],
        WeekTotalHours: "15.5",
        BillableHours: "13.5",
        GeneratedAtUtc: new DateTimeOffset(2026, 3, 8, 9, 5, 0, TimeSpan.Zero),
        ApplicationVersionText: "TempestOS 0.21.0 (2e655db)");
}
