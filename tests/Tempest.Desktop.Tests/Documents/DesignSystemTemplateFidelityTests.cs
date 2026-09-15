using Tempest.Desktop.Documents;

namespace Tempest.Desktop.Tests.Documents;

/// <summary>
/// The template fidelity hook `WP 20.10G` (scope item 4, PO finding D4)
/// asks for: once the Product Owner's own design system export lands in
/// the repository (<c>docs/design/templates/&lt;name&gt;/*.dc.html</c>,
/// <c>docs/design/system/tokens/*.css</c>), this asserts
/// <see cref="DocumentTemplate"/>'s own colour tokens and type family
/// names genuinely appear in the design system's own token files, and
/// that the template folder each renderer is mapped against — the
/// quotation sheet against <c>cost-estimate</c>, the issue sheet against
/// <c>letterhead</c> (see this Work Package's own report for the full
/// mapping and why) — really exists. Every assertion is a no-op,
/// disclosed on <see cref="Xunit.Abstractions.ITestOutputHelper"/>, when
/// the design system has not landed in the tree this test runs against;
/// xunit 2.9.3 (this project's own package version) has no runtime-
/// conditional "Skipped" outcome to report through instead — `[Fact(Skip
/// = ...)]` only accepts a compile-time constant — so a still-empty
/// folder is a passing run with a stated reason, not a literal "Skipped"
/// row, until such a mechanism is available without adding a package
/// this Work Package's brief does not name.
/// </summary>
public sealed class DesignSystemTemplateFidelityTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public DesignSystemTemplateFidelityTests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;

    private static string SystemTokensDirectory => Path.Combine(DesktopTestHelpers.RepositoryRoot, "docs", "design", "system", "tokens");

    private static string TemplatesDirectory => Path.Combine(DesktopTestHelpers.RepositoryRoot, "docs", "design", "templates");

    /// <summary>
    /// Whether the design system export has actually landed — at least
    /// one <c>*.dc.html</c> template file somewhere under
    /// <see cref="TemplatesDirectory"/>. Checked by file, not merely by
    /// the directory's own existence: this Work Package's own
    /// `docs/design/templates/README.md` (scope item 6) lives in that
    /// same directory and must never itself be read as "the export has
    /// landed".
    /// </summary>
    private static bool TemplatesHaveLanded() =>
        Directory.Exists(TemplatesDirectory) && Directory.EnumerateFiles(TemplatesDirectory, "*.dc.html", SearchOption.AllDirectories).Any();

    [Fact]
    public void ColorsCss_WhenPresent_CarriesEveryTemplateColourTokenVerbatim()
    {
        var path = Path.Combine(SystemTokensDirectory, "colors.css");
        if (!File.Exists(path))
        {
            _output.WriteLine($"Skipped: '{path}' is not present in this worktree — the design system export has not (yet) landed on this branch.");
            return;
        }

        var css = File.ReadAllText(path);
        foreach (var token in DocumentTemplate.ReferenceColourTokens)
            Assert.Contains(token.Hex, css, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TypographyCss_WhenPresent_CarriesEveryTemplateTypeFamilyName()
    {
        var path = Path.Combine(SystemTokensDirectory, "typography.css");
        if (!File.Exists(path))
        {
            _output.WriteLine($"Skipped: '{path}' is not present in this worktree — the design system export has not (yet) landed on this branch.");
            return;
        }

        var css = File.ReadAllText(path);
        foreach (var family in DocumentTemplate.ReferenceTypeNames)
            Assert.Contains(family, css, StringComparison.Ordinal);
    }

    /// <summary>The mapping this Work Package implemented — a renderer's own document type to the design system's own template folder name (`_ds_manifest.json`'s own <c>folder</c> field). See the report for why each pairing was chosen.</summary>
    public static TheoryData<string, string> RendererTemplateMapping => new()
    {
        { "QuotationSheetRenderer (the quotation sheet)", "cost-estimate" },
        { "IssueSheetRenderer (the issue sheet)", "letterhead" },
    };

    [Theory]
    [MemberData(nameof(RendererTemplateMapping))]
    public void MappedTemplateFolder_WhenTheDesignSystemHasLanded_Exists(string renderer, string templateFolderName)
    {
        if (!TemplatesHaveLanded())
        {
            _output.WriteLine($"Skipped: no '*.dc.html' template found under '{TemplatesDirectory}' — {renderer}'s own mapped template ('{templateFolderName}') cannot be checked until the design system export lands on this branch.");
            return;
        }

        var folder = Path.Combine(TemplatesDirectory, templateFolderName);
        Assert.True(Directory.Exists(folder), $"{renderer} is mapped to 'docs/design/templates/{templateFolderName}', which does not exist.");
    }
}
