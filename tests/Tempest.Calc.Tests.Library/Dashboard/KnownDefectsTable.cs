using System.Text.RegularExpressions;
using Tempest.Calc.Library.Dashboard;

namespace Tempest.Calc.Tests.Library.Dashboard;

/// <summary>
/// A second, independent reader of <c>docs/testing/v0.26.0-known-defects.md</c> (the first is <c>scripts/generate-verification-snapshot.py</c>).
/// The tables list one finding per row under a Critical, High or Medium heading; the Decision cell says Fixed, Owner decision, Still open or Not a
/// defect; a line "Still open, code unchanged (n): id, id, ..." lists more Medium rows. <see cref="VerificationSnapshotStaleTests"/> checks the
/// snapshot's statuses against this reader on the real document, and the service tests build their sample data from it.
/// </summary>
internal static partial class KnownDefectsTable
{
    internal sealed record Row(string Id, string Severity, VerificationDefectStatus Status);

    [GeneratedRegex(@"^[a-z][a-z0-9.\-]*-[A-Za-z0-9]+$")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"^Still open, code unchanged \((\d+)\):\s*(.*)$")]
    private static partial Regex UnchangedLine();

    public static IReadOnlyList<Row> Parse(string markdown)
    {
        var rows = new List<Row>();
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

            var m = UnchangedLine().Match(line);

            if (m.Success)
            {
                foreach (var id in m.Groups[2].Value.TrimEnd('.').Split(',').Select(i => i.Trim()).Where(i => i.Length > 0))
                    rows.Add(new Row(id, "Medium", VerificationDefectStatus.StillOpen));
            }
        }

        return rows;
    }

    private static VerificationDefectStatus Classify(string decision)
    {
        var text = decision.Replace("*", string.Empty, StringComparison.Ordinal).Trim().ToLowerInvariant();
        return text.StartsWith("not a defect", StringComparison.Ordinal) ? VerificationDefectStatus.NotADefect
            : text.Contains("owner decision", StringComparison.Ordinal) ? VerificationDefectStatus.OwnerDecision
            : text.StartsWith("fixed", StringComparison.Ordinal) ? VerificationDefectStatus.Fixed
            : text.StartsWith("still open", StringComparison.Ordinal) ? VerificationDefectStatus.StillOpen
            : throw new InvalidOperationException($"Cannot classify '{decision}'.");
    }
}
