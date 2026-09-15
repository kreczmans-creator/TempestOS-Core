using System.Runtime.Versioning;
using System.Text;
using Avalonia.Headless.XUnit;
using PDFtoImage;
using Tempest.Desktop.Documents.ProgressReports;
using Tempest.Desktop.Tests.Quotations;

namespace Tempest.Desktop.Tests.Documents;

/// <summary>The progress report document renderer (`WP 21.2A`, scope item 2) — A4 landscape, one section per page (the design system's own "deck" grammar).</summary>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class ProgressReportDocumentRendererTests
{
    [Fact]
    public void DocumentType_And_TemplateName_MatchTheDesignSystemMapping()
    {
        var renderer = new ProgressReportDocumentRenderer();
        Assert.Equal("PROGRESS REPORT", renderer.DocumentType);
        Assert.Equal("progress-report", renderer.TemplateName);
    }

    [AvaloniaFact]
    public void Render_ProducesAValidFourPagePdf_OnePerSection()
    {
        var bytes = new ProgressReportDocumentRenderer().Render(ProgressReportDocumentModelFixtures.Minimal()).ToArray();

        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes, 0, Math.Min(8, bytes.Length)), StringComparison.Ordinal);
        Assert.Equal(4, Conversion.GetPageCount(bytes));
    }

    [AvaloniaFact]
    public void Render_EveryFieldTheReportNames_IsReadableBackOutOfTheText()
    {
        var model = ProgressReportDocumentModelFixtures.Minimal();
        var bytes = new ProgressReportDocumentRenderer().Render(model).ToArray();
        var text = PdfTextExtractor.ExtractText(bytes);

        Assert.Contains("PROGRESS REPORT", text, StringComparison.Ordinal);
        Assert.Contains(model.ProjectCode, text, StringComparison.Ordinal);
        Assert.Contains(model.ProjectName, text, StringComparison.Ordinal);
        Assert.Contains(model.HealthStatus, text, StringComparison.Ordinal);
        Assert.Contains(model.HealthReason, text, StringComparison.Ordinal);
        Assert.Contains(model.DeliverablesNote, text, StringComparison.Ordinal);

        foreach (var milestone in model.Milestones)
            Assert.Contains(milestone.Title, text, StringComparison.Ordinal);

        foreach (var risk in model.Risks)
            Assert.Contains(risk.Title, text, StringComparison.Ordinal);

        Assert.Contains("Page 4 of 4", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1, ProgressReportRag.Overdue)]
    [InlineData(0, ProgressReportRag.AtRisk)]
    [InlineData(7, ProgressReportRag.AtRisk)]
    [InlineData(8, ProgressReportRag.OnTrack)]
    public void RagFor_BucketsByDaysFromToday(int daysFromToday, ProgressReportRag expected)
    {
        var today = new DateOnly(2026, 3, 4);
        Assert.Equal(expected, ProgressReportDocumentModel.RagFor(today.AddDays(daysFromToday), today));
    }

    [AvaloniaFact]
    public void Render_NoRisks_SaysSoRatherThanAnEmptyTable()
    {
        var model = ProgressReportDocumentModelFixtures.Minimal() with { Risks = [] };
        var bytes = new ProgressReportDocumentRenderer().Render(model).ToArray();
        var text = PdfTextExtractor.ExtractText(bytes);

        Assert.Contains("No live risks recorded", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Render_NullModel_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new ProgressReportDocumentRenderer().Render(null!));
}

/// <summary>Fixture <see cref="ProgressReportDocumentModel"/>s for the renderer tests.</summary>
internal static class ProgressReportDocumentModelFixtures
{
    public static ProgressReportDocumentModel Minimal()
    {
        var today = new DateOnly(2026, 3, 4);
        var milestones = new[]
        {
            new ProgressReportMilestoneRow("Design freeze", today.AddDays(-3), ProgressReportRag.Overdue),
            new ProgressReportMilestoneRow("Fabrication complete", today.AddDays(5), ProgressReportRag.AtRisk),
            new ProgressReportMilestoneRow("Site installation", today.AddDays(60), ProgressReportRag.OnTrack),
        };

        return new ProgressReportDocumentModel(
            ProjectCode: "PRJ-100",
            ProjectName: "Acme Gantry Upgrade",
            AsOfDate: today,
            HealthStatus: "Overdue",
            HealthReason: "Overdue: milestone 'Design freeze' was due 2026-03-01.",
            Milestones: milestones,
            LookAheadMilestones: [milestones[1]],
            QuotedHours: 120m,
            RecordedHours: 96m,
            Risks: [new ProgressReportRiskRow("Crane availability at site", "Open", "Possible", "Major")],
            DeliverablesNote: "Deliverable-level progress is not available from the project status summary this report renders from.",
            GeneratedAtUtc: new DateTimeOffset(2026, 3, 4, 9, 5, 0, TimeSpan.Zero),
            ApplicationVersionText: "TempestOS 0.21.0 (2e655db)");
    }
}
