using System.Globalization;
using Tempest.Calc.Sheets;

namespace Tempest.Desktop.Tests.Soak;

/// <summary>
/// What a scenario found out: the facts it measured (counts, per-step times, sizes) and every expectation that did not hold. A scenario
/// keeps going after a failed expectation where it can, so one run reports everything that is wrong, not only the first thing.
/// </summary>
internal sealed class SoakOutcome
{
    /// <summary>More failures than this are counted but not kept, so a systematic defect does not fill the report.</summary>
    public const int MaxKeptFailures = 40;

    private readonly List<string> _failures = [];
    private readonly List<(string Key, string Value)> _facts = [];

    public IReadOnlyList<string> Failures => _failures;

    public int FailureCount { get; private set; }

    public IReadOnlyList<(string Key, string Value)> Facts => _facts;

    /// <summary>The number of expectations checked, failed or not.</summary>
    public int Checks { get; private set; }

    public void Expect(bool condition, Func<string> because)
    {
        Checks++;

        if (condition)
            return;

        FailureCount++;

        if (_failures.Count < MaxKeptFailures)
            _failures.Add(because());
    }

    public void Expect(bool condition, string because) => Expect(condition, () => because);

    public void Fact(string key, string value) => _facts.Add((key, value));

    public void Fact(string key, double value, string format = "0.###") => Fact(key, value.ToString(format, CultureInfo.InvariantCulture));

    public void Fact(string key, long value) => Fact(key, value.ToString("N0", CultureInfo.InvariantCulture));

    public string? FactValue(string key) => _facts.FirstOrDefault(f => f.Key == key).Value;
}

/// <summary>The worked-example inputs shipped under docs/worked-examples, by template: the inputs the soak creates every calculation from.</summary>
internal static class SoakExamples
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<(string Name, IReadOnlyList<CalcInputValue> Inputs)>>> Cache = new(Load);

    /// <summary>The examples of a template (its folder name under the discipline), in name order.</summary>
    public static IReadOnlyList<(string Name, IReadOnlyList<CalcInputValue> Inputs)> For(string templateId) =>
        Cache.Value.TryGetValue(templateId, out var list) ? list : [];

    public static int TemplateCount => Cache.Value.Count;

    private static IReadOnlyDictionary<string, IReadOnlyList<(string Name, IReadOnlyList<CalcInputValue> Inputs)>> Load()
    {
        var root = Path.Combine(DesktopTestHelpers.RepositoryRoot, "docs", "worked-examples");
        var result = new Dictionary<string, List<(string, IReadOnlyList<CalcInputValue>)>>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(root, "inputs.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(root, Path.GetDirectoryName(file)!).Replace('\\', '/');

            // Pack worked examples hold every step of a chain; they are not one sheet's inputs.
            if (relative.StartsWith("packs/", StringComparison.Ordinal))
                continue;

            var parts = relative.Split('/');
            var (inputs, notices) = CalcInputJson.Read(File.ReadAllText(file));

            if (notices.Count > 0)
                throw new InvalidOperationException($"The worked example '{relative}' no longer reads cleanly: {notices[0].Message}");

            if (!result.TryGetValue(parts[1], out var list))
                result[parts[1]] = list = [];

            list.Add((parts[^1], [.. inputs.Values]));
        }

        return result.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<(string Name, IReadOnlyList<CalcInputValue> Inputs)>)kv.Value, StringComparer.Ordinal);
    }
}
