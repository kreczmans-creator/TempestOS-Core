using System.Runtime.Versioning;
using System.Text;
using Avalonia.Headless.XUnit;
using PDFtoImage;
using Tempest.Desktop.Documents.TechnicalReports;
using Tempest.Desktop.Tests.Quotations;

namespace Tempest.Desktop.Tests.Documents;

/// <summary>The technical report document renderer (`WP 21.2A`, scope item 2) — cover block, revision history, numbered sections split from a Document revision's own content.</summary>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class TechnicalReportDocumentRendererTests
{
    [Fact]
    public void DocumentType_And_TemplateName_MatchTheDesignSystemMapping()
    {
        var renderer = new TechnicalReportDocumentRenderer();
        Assert.Equal("TECHNICAL REPORT", renderer.DocumentType);
        Assert.Equal("technical-report", renderer.TemplateName);
    }

    [AvaloniaFact]
    public void Render_ProducesAValidOnePagePdf()
    {
        var bytes = new TechnicalReportDocumentRenderer().Render(TechnicalReportDocumentModelFixtures.Minimal()).ToArray();

        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes, 0, Math.Min(8, bytes.Length)), StringComparison.Ordinal);
        Assert.True(Conversion.GetPageCount(bytes) >= 1);
    }

    [AvaloniaFact]
    public void Render_EveryFieldTheReportNames_IsReadableBackOutOfTheText()
    {
        var model = TechnicalReportDocumentModelFixtures.Minimal();
        var bytes = new TechnicalReportDocumentRenderer().Render(model).ToArray();
        var text = PdfTextExtractor.ExtractText(bytes);

        Assert.Contains("TECHNICAL REPORT", text, StringComparison.Ordinal);
        Assert.Contains(model.Title, text, StringComparison.Ordinal);
        Assert.Contains(model.ProjectCode, text, StringComparison.Ordinal);
        Assert.Contains(model.ProjectName, text, StringComparison.Ordinal);
        Assert.Contains(model.Reference!, text, StringComparison.Ordinal);
        Assert.Contains(model.Author, text, StringComparison.Ordinal);

        foreach (var revision in model.Revisions)
            Assert.Contains(revision.ChangeSummary!, text, StringComparison.Ordinal);

        foreach (var section in model.Sections)
        {
            Assert.Contains(section.Heading!, text, StringComparison.Ordinal);
            Assert.Contains(section.Body, text, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("# First\nBody one.\n# Second\nBody two.", 2, "First", "Second")]
    [InlineData("Just plain content, no headings at all.", 1, "Content", "Content")]
    public void SplitIntoSections_SplitsOnLeadingHashMarkers(string content, int expectedCount, string firstHeading, string lastHeading)
    {
        var sections = TechnicalReportDocumentRenderer.SplitIntoSections(content);

        Assert.Equal(expectedCount, sections.Count);
        Assert.Equal(firstHeading, sections[0].Heading);
        Assert.Equal(lastHeading, sections[^1].Heading);
    }

    [AvaloniaFact]
    public void Render_NoRevisions_SaysSoRatherThanAnEmptyTable()
    {
        var model = TechnicalReportDocumentModelFixtures.Minimal() with { Revisions = [] };
        var bytes = new TechnicalReportDocumentRenderer().Render(model).ToArray();
        var text = PdfTextExtractor.ExtractText(bytes);

        Assert.Contains("No revisions recorded", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Render_NullModel_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new TechnicalReportDocumentRenderer().Render(null!));
}

/// <summary>Fixture <see cref="TechnicalReportDocumentModel"/>s for the renderer tests.</summary>
internal static class TechnicalReportDocumentModelFixtures
{
    public static TechnicalReportDocumentModel Minimal() => new(
        Title: "Gantry Structural Assessment",
        Reference: "DOC-0042",
        ProjectCode: "PRJ-100",
        ProjectName: "Acme Gantry Upgrade",
        Author: "priya.patel",
        Revisions:
        [
            new TechnicalReportRevisionRow(1, new DateOnly(2026, 2, 20), "priya.patel", "Initial issue."),
            new TechnicalReportRevisionRow(2, new DateOnly(2026, 3, 1), "priya.patel", "Updated load case per client comment."),
        ],
        Sections: TechnicalReportDocumentRenderer.SplitIntoSections(
            "# Introduction\nThis report assesses the structural adequacy of the gantry.\n# Loading\nDead and live loads per BS EN 1991."),
        GeneratedAtUtc: new DateTimeOffset(2026, 3, 4, 9, 5, 0, TimeSpan.Zero),
        ApplicationVersionText: "TempestOS 0.21.0 (2e655db)");
}
