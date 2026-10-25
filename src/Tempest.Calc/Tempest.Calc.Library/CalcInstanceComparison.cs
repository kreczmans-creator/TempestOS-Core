using System.Globalization;
using System.Text;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>What a comparison row compares.</summary>
public enum CalcDifferenceKind
{
    Input,
    Output,
    Check,
}

/// <summary>One value that differs between two calculations; "—" stands for a value one side does not have.</summary>
/// <param name="Change">The relative change of a number, after against before, when both sides have one and the earlier is not zero.</param>
public sealed record CalcDifference(CalcDifferenceKind Kind, string Symbol, string Description, string Before, string After, double? Change = null);

/// <summary>
/// Two calculations of one template set side by side (ADR-0167 §4: the
/// legacy surface's Compare, reimplemented over calc instances): every
/// input, output and check whose value differs, inputs first.
/// </summary>
public sealed record CalcComparison(CalcInstance Before, CalcInstance After, IReadOnlyList<CalcDifference> Rows, int Omitted = 0)
{
    public bool HasChanges => Rows.Count > 0;

    /// <summary>The comparison as plain text: a heading line, then one line per difference.</summary>
    public string Text
    {
        get
        {
            var sb = new StringBuilder();
            sb.Append(CultureInfo.InvariantCulture, $"{Before.Name} rev {Before.Revision} → {After.Name} rev {After.Revision}");

            if (!string.Equals(Before.TemplateReference, After.TemplateReference, StringComparison.Ordinal))
                sb.Append(CultureInfo.InvariantCulture, $" (template {Before.TemplateReference} → {After.TemplateReference})");

            sb.Append('\n');

            if (!HasChanges)
                return sb.Append("Nothing differs.\n").ToString();

            foreach (var r in Rows)
            {
                sb.Append(CultureInfo.InvariantCulture, $"{r.Kind,-6}  {r.Symbol}: {r.Before} → {r.After}");

                if (r.Change is { } c)
                    sb.Append(CultureInfo.InvariantCulture, $"  ({(c >= 0 ? "+" : string.Empty)}{CalcValue.FormatNumber(100 * c, 3)} %)");

                sb.Append('\n');
            }

            if (Omitted > 0)
                sb.Append(CultureInfo.InvariantCulture, $"… and {Omitted} more difference(s).\n");

            return sb.ToString();
        }
    }
}

/// <summary>Builds a <see cref="CalcComparison"/>. Pure: reads the two snapshots and nothing else.</summary>
public static class CalcInstanceComparer
{
    /// <summary>Rows kept in one comparison; the rest are counted (§3.8 caps).</summary>
    public const int MaxRows = 500;

    /// <summary>Compares <paramref name="before"/> with <paramref name="after"/>; both must be calculations of one template (any version).</summary>
    /// <exception cref="ArgumentException">The two are of different templates, or either is a legacy archive.</exception>
    public static CalcComparison Compare(CalcInstance before, CalcInstance after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        if (before.IsLegacyArchive || after.IsLegacyArchive)
            throw new ArgumentException("A legacy archive is a read-only record of a calculation the current templates did not run; it is not compared value by value.");

        if (!string.Equals(before.TemplateId, after.TemplateId, StringComparison.Ordinal))
            throw new ArgumentException($"'{before.Name}' is a {before.TemplateId} calculation and '{after.Name}' a {after.TemplateId} one; only calculations of one template are compared.");

        var rows = new List<CalcDifference>();
        var omitted = 0;

        void Add(CalcDifference d)
        {
            if (rows.Count < MaxRows)
                rows.Add(d);
            else
                omitted++;
        }

        foreach (var symbol in Union(before.Inputs.Select(i => i.Symbol), after.Inputs.Select(i => i.Symbol)))
        {
            var b = before.Inputs.FirstOrDefault(i => i.Symbol == symbol);
            var a = after.Inputs.FirstOrDefault(i => i.Symbol == symbol);

            if (a?.Rows is not null || b?.Rows is not null)
            {
                CompareRows(symbol, b?.Rows ?? [], a?.Rows ?? [], Add);
                continue;
            }

            var bt = InputText(b);
            var at = InputText(a);

            if (!string.Equals(bt, at, StringComparison.Ordinal))
                Add(new CalcDifference(CalcDifferenceKind.Input, symbol, string.Empty, bt, at, Change(b?.Number, a?.Number, b?.Unit == a?.Unit)));
        }

        foreach (var symbol in Union(before.Outputs.Select(o => o.Symbol), after.Outputs.Select(o => o.Symbol)))
        {
            var b = before.Outputs.FirstOrDefault(o => o.Symbol == symbol);
            var a = after.Outputs.FirstOrDefault(o => o.Symbol == symbol);
            var bt = b?.Text ?? "—";
            var at = a?.Text ?? "—";

            if (!string.Equals(bt, at, StringComparison.Ordinal))
                Add(new CalcDifference(CalcDifferenceKind.Output, symbol, (a ?? b)!.Description, bt, at, Change(b?.Si, a?.Si, true)));
        }

        foreach (var name in Union(before.Checks.Select(c => c.Name), after.Checks.Select(c => c.Name)))
        {
            var b = before.Checks.FirstOrDefault(c => c.Name == name);
            var a = after.Checks.FirstOrDefault(c => c.Name == name);
            var bt = CheckText(b);
            var at = CheckText(a);

            if (!string.Equals(bt, at, StringComparison.Ordinal))
                Add(new CalcDifference(CalcDifferenceKind.Check, name, string.Empty, bt, at));
        }

        return new CalcComparison(before, after, rows, omitted);
    }

    /// <summary>The later calculation's order first, then anything only the earlier one had.</summary>
    private static IEnumerable<string> Union(IEnumerable<string> before, IEnumerable<string> after) =>
        after.Concat(before).Distinct(StringComparer.Ordinal);

    private static string InputText(CalcInputValue? v) =>
        v is null ? "—" : v.Unit is { Length: > 0 } u ? $"{v.ValueText} {u}" : v.ValueText;

    private static string CheckText(CalcCheckSnapshot? c) =>
        c is null ? "—" : $"{(c.Passed ? "PASS" : "FAIL")} {c.Demand}";

    private static double? Change(double? before, double? after, bool sameUnit) =>
        sameUnit && before is { } b && after is { } a && b != 0 && double.IsFinite(a) && double.IsFinite(b) ? (a - b) / Math.Abs(b) : null;

    private static void CompareRows(string symbol, IReadOnlyList<CalcRow> before, IReadOnlyList<CalcRow> after, Action<CalcDifference> add)
    {
        for (var i = 0; i < Math.Max(before.Count, after.Count); i++)
        {
            var bt = i < before.Count ? RowText(before[i]) : "—";
            var at = i < after.Count ? RowText(after[i]) : "—";

            if (!string.Equals(bt, at, StringComparison.Ordinal))
                add(new CalcDifference(CalcDifferenceKind.Input, string.Create(CultureInfo.InvariantCulture, $"{symbol}[{i + 1}]"), string.Empty, bt, at));
        }
    }

    private static string RowText(CalcRow row) =>
        string.Join("; ", row.Cells.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => $"{c.Key} = {CellText(c.Value)}"));

    private static string CellText(CalcCell cell) => cell switch
    {
        { Number: { } n } => CalcValue.FormatNumber(n, 6) + (cell.Unit is { Length: > 0 } u ? " " + u : string.Empty),
        { Text: { } t } => t,
        { Flag: { } f } => f ? "yes" : "no",
        _ => "—",
    };
}
