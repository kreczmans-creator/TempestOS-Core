using System.Text;
using System.Text.RegularExpressions;
using Tempest.Calc.Library.Dashboard;

namespace Tempest.Calc.Tests.Library.Dashboard;

/// <summary>
/// A second, independent reader of <c>docs/testing/v0.26.0-known-defects.md</c> (the first is <c>scripts/generate-verification-snapshot.py</c>).
/// The tables list one finding per row under a Critical, High or Medium heading; the Decision cell says Fixed, Owner decision, Still open (or
/// Partly fixed) or Not a defect; a line "Still open, code unchanged (n): id, id, ..." or "Still open, listed for the owner (n): id (a note)."
/// lists more Medium rows. Like the script, it checks the rows it read against the summary table the document prints and refuses a document
/// where they differ. <see cref="VerificationSnapshotStaleTests"/> checks the snapshot's statuses against this reader on the real document,
/// and the service tests build their sample data from it.
/// </summary>
internal static partial class KnownDefectsTable
{
    internal sealed record Row(string Id, string Severity, VerificationDefectStatus Status);

    [GeneratedRegex(@"^[a-z][a-z0-9.\-]*-[A-Za-z0-9]+$")]
    private static partial Regex IdPattern();

    /// <summary>An id at the start of a bulk-line item; a backtick before it is allowed, whatever follows it (a bracketed note) is not part of it.</summary>
    [GeneratedRegex(@"^`?([a-z][a-z0-9.\-]*-[A-Za-z0-9]+)`?")]
    private static partial Regex IdAtStart();

    [GeneratedRegex(@"^Still open, [^(]*\((\d+)\):\s*(.*)$")]
    private static partial Regex BulkLine();

    [GeneratedRegex(@"^\|\s*(Critical|High|Medium)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|")]
    private static partial Regex SummaryRow();

    public static IReadOnlyList<Row> Parse(string markdown)
    {
        var rows = new List<Row>();
        var summary = new Dictionary<string, int[]>(StringComparer.Ordinal);
        string? section = null;

        foreach (var raw in markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.TrimEnd();

            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                section = new[] { "Critical", "High", "Medium" }.FirstOrDefault(s => line.StartsWith("### " + s, StringComparison.Ordinal));
                continue;
            }

            if (section is not null && line.StartsWith('|'))
            {
                var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();

                if (cells.Length < 3 || !IdPattern().IsMatch(cells[0]))
                    continue;

                var decision = section == "Medium" ? cells[1] : cells[2];
                rows.Add(new Row(cells[0], section, Classify(decision)));
            }

            var s = SummaryRow().Match(line);

            if (s.Success)
                summary[s.Groups[1].Value] = [.. Enumerable.Range(2, 5).Select(i => int.Parse(s.Groups[i].Value, System.Globalization.CultureInfo.InvariantCulture))];

            var m = BulkLine().Match(line);

            if (m.Success)
            {
                var label = line[..line.IndexOf('(', StringComparison.Ordinal)].Trim();
                var ids = BulkIds(m.Groups[2].Value);

                if (ids.Count != int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
                    throw new InvalidOperationException($"The '{label}' line says {m.Groups[1].Value} ids and lists {ids.Count}.");

                foreach (var id in ids)
                {
                    if (rows.Any(r => r.Id == id))
                        throw new InvalidOperationException($"{id} is in a table and in the '{label}' line.");

                    rows.Add(new Row(id, "Medium", VerificationDefectStatus.StillOpen));
                }
            }
        }

        CrossCheck(rows, summary);
        return rows;
    }

    /// <summary>The ids of a bulk line: split at commas outside brackets, each id read from the start of its item (backticks and a trailing bracketed note dropped).</summary>
    private static List<string> BulkIds(string text)
    {
        var items = new List<string>();
        var current = new StringBuilder();
        var depth = 0;

        foreach (var c in text.Trim().TrimEnd('.'))
        {
            if (c == '(')
                depth++;
            else if (c == ')')
                depth = Math.Max(0, depth - 1);

            if (c == ',' && depth == 0)
            {
                items.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        items.Add(current.ToString());
        var ids = new List<string>();

        foreach (var item in items.Select(i => i.Trim()).Where(i => i.Length > 0))
        {
            var m = IdAtStart().Match(item);

            if (!m.Success)
                throw new InvalidOperationException($"'{item[..Math.Min(40, item.Length)]}' in a 'Still open' line does not start with a finding id.");

            ids.Add(m.Groups[1].Value);
        }

        return ids;
    }

    /// <summary>The rows read must add up to the summary table's counts (fixed, owner decision, still open, not a defect, total), as the script checks.</summary>
    private static void CrossCheck(List<Row> rows, Dictionary<string, int[]> summary)
    {
        foreach (var (severity, printed) in summary)
        {
            var of = rows.Where(r => r.Severity == severity).ToList();
            int[] counted =
            [
                of.Count(r => r.Status == VerificationDefectStatus.Fixed),
                of.Count(r => r.Status == VerificationDefectStatus.OwnerDecision),
                of.Count(r => r.Status == VerificationDefectStatus.StillOpen),
                of.Count(r => r.Status == VerificationDefectStatus.NotADefect),
                of.Count,
            ];

            if (!counted.SequenceEqual(printed))
                throw new InvalidOperationException($"The {severity} rows read as [{string.Join(", ", counted)}] but the summary table prints [{string.Join(", ", printed)}] (fixed, owner decision, still open, not a defect, total).");
        }
    }

    private static VerificationDefectStatus Classify(string decision)
    {
        var text = decision.Replace("*", string.Empty, StringComparison.Ordinal).Trim().ToLowerInvariant();
        return text.StartsWith("not a defect", StringComparison.Ordinal) ? VerificationDefectStatus.NotADefect
            : text.Contains("owner decision", StringComparison.Ordinal) ? VerificationDefectStatus.OwnerDecision
            : text.StartsWith("fixed", StringComparison.Ordinal) ? VerificationDefectStatus.Fixed
            : text.StartsWith("still open", StringComparison.Ordinal) || text.StartsWith("partly fixed", StringComparison.Ordinal) ? VerificationDefectStatus.StillOpen
            : throw new InvalidOperationException($"Cannot classify '{decision}'.");
    }
}
