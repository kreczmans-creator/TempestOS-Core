using Tempest.Core.Tests.Templates;

namespace Tempest.Core.Tests.Invoicing.Xero.Live;

/// <summary>
/// X8's documents sit where design §11 says X8 owns them (`v0.24.0`
/// Technical Design, the X8 row): the runbook is
/// <c>docs/releases/v0.24.0/PO Test Runbook.md</c>, every document names it
/// there, and the Xero steps live in it rather than in
/// <c>PHYSICAL_REVIEW.md</c>, which is outside X8's row.
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
    public void TheXeroSteps_LiveInTheRunbook_NotInPhysicalReview()
    {
        var review = File.ReadAllText(Path.Combine(Root, "PHYSICAL_REVIEW.md"));
        var runbook = File.ReadAllText(Path.Combine(Root, RunbookPath));

        Assert.DoesNotContain("### 7m. Xero", review, StringComparison.Ordinal);
        Assert.DoesNotContain("| XR-1 |", review, StringComparison.Ordinal);
        foreach (var section in new[] { "## XA.", "## XS.", "## XC.", "## XQ.", "## XI.", "## XP.", "## XE.", "## XR.", "## XO.", "## XG.", "## XF.", "## XL." })
            Assert.Contains(section, runbook, StringComparison.Ordinal);
    }
}
