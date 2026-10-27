using System.Text;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library.RecordImport;

/// <summary>Shared pieces for the record import tests: the host over every shipped calculator, the repository's template folders, and file builders.</summary>
internal static class RecordImportTestSupport
{
    public static LibraryTestHost Host() => new(calculators: ShippedCalculators.All);

    /// <summary>The host with the lifting flag on, so the lifting calculator and its three record kinds are visible (ADR-0173).</summary>
    public static LibraryTestHost HostWithLifting() => new(calculators: ShippedCalculators.All, features: new CalcFeatureFlags([CalcFeatures.Lifting]));

    /// <summary>The repository root: the nearest folder above the test binaries that holds the VERSION file.</summary>
    public static string RepositoryRoot { get; } = FindRoot();

    /// <summary>Track P's templates 01 to 82 (read-only).</summary>
    public static string TemplateFolder => Path.Combine(RepositoryRoot, "docs", "verification", "record-templates");

    /// <summary>Track S's templates 83 and up (DC-10).</summary>
    public static string TrackSTemplateFolder => Path.Combine(RepositoryRoot, "docs", "calcs", "record-templates");

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(dir.FullName, "VERSION")) && Directory.Exists(Path.Combine(dir.FullName, "docs")))
                return dir.FullName;
        }

        throw new InvalidOperationException("The repository root (the folder with VERSION and docs) was not found above " + AppContext.BaseDirectory);
    }

    public static byte[] Utf8(string text) => new UTF8Encoding(false).GetBytes(text);

    /// <summary>A file of the template's header row followed by the given data lines.</summary>
    public static byte[] Csv(string header, params string[] lines) => Utf8(header + "\r\n" + string.Join("\r\n", lines) + (lines.Length > 0 ? "\r\n" : string.Empty));

    /// <summary>Provenance with every required field; the source is licensed unless <paramref name="licensed"/> says otherwise.</summary>
    public static RecordProvenanceInput Provenance(string title = "Test table", bool licensed = true, string amendment = "A2:2022") =>
        new(title, "Test Wiring Rules", "2018", amendment, "Table 4D2A", "Own copy (test)", licensed);

    public static RecordImportRequest Request(string templateId, byte[] bytes, RecordProvenanceInput? provenance = null, string? fileName = "test.csv") =>
        new(bytes, fileName, templateId, provenance ?? Provenance());

    public static string Describe(RecordImportPreview preview) =>
        string.Join(" | ", new[] { preview.Refusal }.Concat(preview.AllIssues.Select(i => i.ToString())).Where(s => s is not null));
}
