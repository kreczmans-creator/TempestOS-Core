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

    /// <summary>What the scenario's repeated work held at its halfway point and at its end, for the check on growth within one host (see <see cref="SoakRunner.InHostBaseBytes"/>); <see langword="null"/> when the scenario did not report its items.</summary>
    public SoakInHostSample? InHost { get; private set; }

    private int _itemsTotal;
    private int _itemsAtHalf = -1;
    private long _heapAtHalf;

    /// <summary>
    /// A scenario that does the same work over and over (calculations created, revisions issued, runs made) calls this after each item, with the number of
    /// items done so far and the number it will do. At the first item at or after the halfway point (and not before <paramref name="warmUp"/> items, for work
    /// that does a different thing for each of its first few, such as one of each template) the settled managed heap is measured, and again after the last
    /// item, both with everything the scenario still holds alive: so the second half's growth is the heap the host and the scenario kept for those items
    /// and is not diluted by the work's one-off set-up. The collections it makes take a moment, so it measures twice and does nothing in between.
    /// </summary>
    public void Item(int done, int total, int warmUp = 0)
    {
        if (total <= 0)
            return;

        _itemsTotal = total;

        if (_itemsAtHalf < 0 && done < total && done >= Math.Max((total + 1) / 2, warmUp))
        {
            _itemsAtHalf = done;
            _heapAtHalf = SoakProbe.SettledHeap();
        }
        else if (done == total && _itemsAtHalf >= 0 && InHost is null)
        {
            InHost = new SoakInHostSample(total, total - _itemsAtHalf, _heapAtHalf, SoakProbe.SettledHeap());
        }
    }

    /// <summary>The number of items the scenario said it would do (0 when it did not say).</summary>
    public int ItemsTotal => _itemsTotal;
}

/// <summary>The settled managed heap of one pass at the halfway point of a scenario's items and after the last one.</summary>
/// <param name="Items">All the items the scenario did.</param>
/// <param name="SecondHalfItems">The items done between the two measurements.</param>
/// <param name="HeapAtHalf">The settled heap after the first half, host running.</param>
/// <param name="HeapAtEnd">The settled heap after the last item, host running.</param>
internal sealed record SoakInHostSample(int Items, int SecondHalfItems, long HeapAtHalf, long HeapAtEnd)
{
    public long SecondHalfGrowth => HeapAtEnd - HeapAtHalf;
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
