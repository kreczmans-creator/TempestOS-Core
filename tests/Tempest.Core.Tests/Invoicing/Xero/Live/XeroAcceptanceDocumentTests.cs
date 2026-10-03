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

    /// <summary>
    /// The go-live gate is the same everywhere (F4 verifier round 1,
    /// defect 2): the runbook's XG7, the step the Product Owner acts on,
    /// names every review-board item the Release Notes and the setup guide
    /// do — M1 (Demo records copied), M2 (expired token offline) and M7
    /// (unit-price rounding) — so it can never clear going live on less.
    /// </summary>
    [Fact]
    public void TheGoLiveGate_NamesM1M2AndM7_InTheRunbookTheNotesAndTheGuide()
    {
        var runbook = File.ReadAllText(Path.Combine(Root, RunbookPath));
        var xg7 = runbook.Split('\n').Single(line => line.StartsWith("| XG7 |", StringComparison.Ordinal));
        var notes = File.ReadAllText(Path.Combine(Root, "docs", "releases", "v0.24.0", "Release Notes.md"));
        var guide = File.ReadAllText(Path.Combine(Root, "docs", "guides", "Xero Setup - Step by Step.md"));

        Assert.Contains("M1, M2 and M7", xg7, StringComparison.Ordinal);
        Assert.Contains("M1, M2 and M7", notes, StringComparison.Ordinal);
        Assert.Contains("M1, M2 and M7", guide, StringComparison.Ordinal);
    }

    /// <summary>
    /// How long a supplied token must have left for <c>-KeyWindow</c>
    /// (F4 verifier round 1, defect 4). xUnit does not fix the order of the
    /// three live tests, so the key-window probe can start last, after both
    /// journeys: the authoriser's 2-minute refresh skew, about 3 minutes for
    /// the hand-built journey, about 2 for the production path (about 49
    /// calls and up to a minute's rate-limit pause) and about 7 for the
    /// probe (a 5½-minute wait plus its calls) make 14. X8's 12 predates the
    /// production path.
    /// </summary>
    internal const int KeyWindowTokenMinutes = 2 + 3 + 2 + 7;

    [Fact]
    public void TheSuppliedTokenMargin_ForKeyWindow_CoversBothJourneysAndTheProbe()
    {
        var script = File.ReadAllText(Path.Combine(Root, "scripts", "xero-demo-smoke.ps1"));
        var guide = File.ReadAllText(Path.Combine(Root, "docs", "guides", "Xero Setup - Step by Step.md"));

        Assert.Contains($"at least {KeyWindowTokenMinutes} minutes left for -KeyWindow", script, StringComparison.Ordinal);
        Assert.Contains($"at least {KeyWindowTokenMinutes} minutes left for `-KeyWindow`", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("12 minutes left", script, StringComparison.Ordinal);
        Assert.DoesNotContain("12 minutes left", guide, StringComparison.Ordinal);
    }

    /// <summary>
    /// The F4 bullet in the Build Decisions signs off every file F4 edited
    /// outside its §11 row (F4 verifier round 1, defect 3).
    /// </summary>
    /// <param name="file">A file F4 edited that another task owns.</param>
    [Theory]
    [InlineData("PHYSICAL_REVIEW.md")]
    [InlineData(".github/workflows/ci.yml")]
    [InlineData("XeroApiSimulator.cs")]
    [InlineData("XeroContactLinkerRound3Tests.cs")]
    [InlineData("ADR-0151")]
    [InlineData("XeroLiveConnection")]
    [InlineData("JournalProblems")]
    [InlineData("XeroLiveSmokeTests")]
    [InlineData("scripts/xero-demo-smoke.ps1")]
    public void TheF4SignOff_NamesEveryFileEditedOutsideItsRow(string file)
    {
        var decisions = File.ReadAllText(Path.Combine(Root, "docs", "releases", "v0.24.0", "Xero Build Decisions.md"));
        var f4 = decisions.Split('\n').Single(line => line.StartsWith("- **F4 ", StringComparison.Ordinal));

        Assert.Contains(file, f4, StringComparison.Ordinal);
    }
}
