using Tempest.Core.Tests.Templates;

namespace Tempest.Core.Tests.Invoicing.Xero.Live;

/// <summary>
/// X8's documents sit where design §11 says X8 owns them (`v0.24.0`
/// Technical Design, the X8 row): the runbook is
/// <c>docs/releases/v0.24.0/PO Test Runbook.md</c>, every document names it
/// there, and the Xero steps live in it. <c>PHYSICAL_REVIEW.md</c> §7m (added
/// by the review board's fix m9, which the Release Notes cite) is a short
/// walk that points at the runbook's steps rather than repeating them.
/// </summary>
public sealed class XeroAcceptanceDocumentTests
{
    private const string RunbookPath = "docs/releases/v0.24.0/PO Test Runbook.md";

    private static string Root => RepositoryPaths.RepositoryRoot;

    [Fact]
    public void TheRunbook_IsAtThePathTheTechnicalDesignGivesX8()
    {
        var design = File.ReadAllText(Path.Combine(Root, "docs", "releases", "v0.24.0", "Xero Technical Design.md"));
        var x8Row = design.Split('\n').Single(line => line.StartsWith("| **X8**", StringComparison.Ordinal));

        Assert.Contains($"`{RunbookPath}`", x8Row, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(Root, RunbookPath)), $"Expected the runbook at '{RunbookPath}'.");
    }

    [Theory]
    [InlineData("PHYSICAL_REVIEW.md")]
    [InlineData("docs/releases/v0.24.0/Release Notes.md")]
    [InlineData("docs/guides/Xero Setup - Step by Step.md")]
    [InlineData("docs/governance/Architecture/ADR Register.md")]
    public void NoDocument_NamesTheRunbookByAnotherPath(string document)
    {
        var text = File.ReadAllText(Path.Combine(Root, document));

        Assert.DoesNotContain("PO Test Runbook - Xero", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAdr_NamesTheRunbookAtItsPath()
    {
        var adr = Assert.Single(Directory.GetFiles(Path.Combine(Root, "docs", "adr"), "ADR-0162-*.md"));
        var text = File.ReadAllText(adr);

        Assert.Contains(RunbookPath, text, StringComparison.Ordinal);
        Assert.DoesNotContain("PO Test Runbook - Xero", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheXeroSteps_LiveInTheRunbook_AndPhysicalReview7m_PointsAtThem()
    {
        var review = File.ReadAllText(Path.Combine(Root, "PHYSICAL_REVIEW.md"));
        var runbook = File.ReadAllText(Path.Combine(Root, RunbookPath));
        var notes = File.ReadAllText(Path.Combine(Root, "docs", "releases", "v0.24.0", "Release Notes.md"));

        // The Release Notes cite §7m, so it exists; it names the runbook and
        // its rows cite runbook steps instead of copying their tables.
        Assert.Contains("PHYSICAL_REVIEW.md` §7m", notes, StringComparison.Ordinal);
        var start = review.IndexOf("### 7m. ", StringComparison.Ordinal);
        Assert.True(start >= 0, "PHYSICAL_REVIEW.md should have a §7m for v0.24.0's Xero walk.");
        var end = review.IndexOf("\n## 8.", start, StringComparison.Ordinal);
        var walk = review[start..end];
        Assert.Contains(RunbookPath, walk, StringComparison.Ordinal);
        Assert.Contains("(runbook X", walk, StringComparison.Ordinal);
        Assert.DoesNotContain("| XQ1 |", walk, StringComparison.Ordinal);
        Assert.DoesNotContain("| XR-1 |", review, StringComparison.Ordinal);
        foreach (var section in new[] { "## XA.", "## XS.", "## XC.", "## XQ.", "## XI.", "## XP.", "## XE.", "## XR.", "## XO.", "## XG.", "## XF.", "## XL." })
            Assert.Contains(section, runbook, StringComparison.Ordinal);
    }
}
