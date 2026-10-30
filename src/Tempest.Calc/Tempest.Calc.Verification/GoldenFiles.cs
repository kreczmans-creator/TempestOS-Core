using Tempest.Calc.Sheets;

namespace Tempest.Calc.Verification;

/// <summary>
/// Golden-file support: renders a sheet to normalised text and compares it
/// with a stored expectation. A test that sets <c>TEMPEST_UPDATE_GOLDEN=1</c>
/// rewrites the file instead of failing, so an intended change is a
/// reviewed diff rather than a hand edit.
/// </summary>
public static class GoldenFiles
{
    public const string UpdateVariable = "TEMPEST_UPDATE_GOLDEN";

    /// <summary>A fixed header and footer so a golden file depends on the template and inputs only.</summary>
    public static CalcSheetHeader FixedHeader(string calcId, string title) =>
        new("GOLDEN", calcId, title, "A", CalcSheetStatus.Draft, "golden", null, null, null, null);

    public static CalcSheetFooter FixedFooter(CalcTemplate template, int passed, int total) =>
        new(template.Reference, "0000000000000000", "engine", passed, total, null);

    /// <summary>Renders a calculator run as normalised golden text.</summary>
    public static string Render(ICalcCalculator calculator, CalcInputSet inputs)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        ArgumentNullException.ThrowIfNull(inputs);

        var template = calculator.Template;
        var result = CalcRunner.Run(calculator, inputs);
        var report = KnownAnswerRunner.Run(calculator);
        var sheet = CalcSheetBuilder.Build(template, inputs, result, FixedHeader(template.Id, template.Title), FixedFooter(template, report.Passed, report.Total));
        return CalcSheetTextRenderer.Normalise(CalcSheetTextRenderer.Render(sheet));
    }

    /// <summary>The path of a golden file beside the calling test source file: <c>&lt;test dir&gt;/Golden/&lt;name&gt;.txt</c>. Golden files live in the source tree and are committed.</summary>
    public static string SourcePath(string name, [System.Runtime.CompilerServices.CallerFilePath] string callerFilePath = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var dir = Path.GetDirectoryName(callerFilePath) ?? AppContext.BaseDirectory;
        return Path.Combine(dir, "Golden", name + ".txt");
    }

    /// <summary>Compares <paramref name="actual"/> with the file at <paramref name="path"/>; writes it when updating or when the file does not exist yet. Returns a diff description or <see langword="null"/> when equal.</summary>
    public static string? Compare(string path, string actual)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(actual);

        var normalisedActual = actual.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";
        var update = Environment.GetEnvironmentVariable(UpdateVariable) == "1";

        if (update || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, normalisedActual);
            return update ? null : $"Golden file created at {path}; re-run to compare.";
        }

        var expected = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";

        if (string.Equals(expected, normalisedActual, StringComparison.Ordinal))
            return null;

        var e = expected.Split('\n');
        var a = normalisedActual.Split('\n');

        for (var i = 0; i < Math.Max(e.Length, a.Length); i++)
        {
            var el = i < e.Length ? e[i] : "<end>";
            var al = i < a.Length ? a[i] : "<end>";

            if (!string.Equals(el, al, StringComparison.Ordinal))
                return $"Golden mismatch at line {i + 1} of {path}:\n  expected: {el}\n  actual:   {al}\nSet {UpdateVariable}=1 to accept.";
        }

        return "Golden mismatch (line endings).";
    }
}
