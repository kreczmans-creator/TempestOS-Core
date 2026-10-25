using System.Globalization;

namespace Tempest.Calc.Import.Scan;

public enum ScanChangeKind
{
    Added,
    Removed,
    Changed,
}

/// <summary>One difference between an earlier import and this one. <see cref="AffectsInput"/> is true when a mapped input moves with it.</summary>
public sealed record ScanChange(ScanChangeKind Kind, string Feature, string Stage, string? Target, string? Before, string? After, bool AffectsInput);

/// <summary>The differences between the previous import through a profile and this one.</summary>
public sealed record ScanImportDiff(bool FirstImport, IReadOnlyList<string> PreviousFiles, IReadOnlyList<ScanChange> Changes)
{
    public bool HasChanges => Changes.Count > 0;

    public int Count(ScanChangeKind kind) => Changes.Count(c => c.Kind == kind);

    /// <summary>A one-line summary for a status bar.</summary>
    public string Summary => FirstImport
        ? "First import through this profile."
        : Changes.Count == 0
            ? "No change since the previous import."
            : string.Create(CultureInfo.InvariantCulture, $"{Count(ScanChangeKind.Added)} added, {Count(ScanChangeKind.Removed)} removed, {Count(ScanChangeKind.Changed)} changed since the previous import.");

    public static ScanImportDiff Compute(ScanImportRecord? previous, IReadOnlyList<ScanImportedFeature> now)
    {
        ArgumentNullException.ThrowIfNull(now);

        if (previous is null)
            return new ScanImportDiff(true, [], []);

        static string KeyOf(ScanImportedFeature f) => f.Feature.ToUpperInvariant() + "\u0001" + f.Stage.ToUpperInvariant();

        var before = previous.Features.GroupBy(KeyOf, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
        var after = now.GroupBy(KeyOf, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
        var changes = new List<ScanChange>();

        foreach (var (key, f) in after)
        {
            if (!before.TryGetValue(key, out var old))
                changes.Add(new ScanChange(ScanChangeKind.Added, f.Feature, f.Stage, f.Target, null, Describe(f), f.Target is not null));
            else if (!Same(old, f))
                changes.Add(new ScanChange(ScanChangeKind.Changed, f.Feature, f.Stage, f.Target ?? old.Target, Describe(old), Describe(f), f.Target is not null || old.Target is not null));
        }

        foreach (var (key, old) in before)
        {
            if (!after.ContainsKey(key))
                changes.Add(new ScanChange(ScanChangeKind.Removed, old.Feature, old.Stage, old.Target, Describe(old), null, old.Target is not null));
        }

        return new ScanImportDiff(false, previous.Files.Select(f => f.Sha256).ToList(), changes.OrderBy(c => c.Feature, StringComparer.Ordinal).ThenBy(c => c.Stage, StringComparer.Ordinal).ToList());
    }

    private static bool Same(ScanImportedFeature a, ScanImportedFeature b) =>
        a.Count == b.Count && string.Equals(a.Target, b.Target, StringComparison.Ordinal) && Close(a.Mean, b.Mean) && Close(a.Sigma, b.Sigma) && Close(a.Nominal, b.Nominal);

    private static bool Close(double? x, double? y)
    {
        if (x is null || y is null)
            return x is null && y is null;

        return x == y || Math.Abs(x.Value - y.Value) <= 1e-12 * Math.Max(Math.Abs(x.Value), Math.Abs(y.Value));
    }

    /// <summary>A fitted feature the way a person reads it: <c>n = 30, mean 51.4855 mm, σ 0.0099 mm</c>.</summary>
    public static string Describe(ScanImportedFeature f)
    {
        ArgumentNullException.ThrowIfNull(f);
        var unit = f.Unit.Length == 0 ? string.Empty : " " + f.Unit;
        return string.Create(CultureInfo.InvariantCulture, $"n = {f.Count}, mean {Engine.Values.CalcValue.FormatNumber(f.Mean, 8)}{unit}, σ {Engine.Values.CalcValue.FormatNumber(f.Sigma, 6)}{unit}");
    }
}
