using System.Globalization;
using System.Text;

namespace Tempest.Calc.Library.Dashboard;

/// <summary>
/// Addendum C, C-4: the verification dashboard as a markdown document for the owner's records. A plain writer: a heading, the filter and
/// the date, a summary line, one table row per calculator or chain, then the open defects row by row and a note on each column. Every
/// cell is single-line text with the characters that would break a table escaped, and the size is capped.
/// </summary>
public static class VerificationMarkdown
{
    /// <summary>The most characters a document holds; the rows beyond it are left out and the document says so.</summary>
    public const int MaxCharacters = 1_000_000;

    /// <summary>The longest cell text kept.</summary>
    public const int MaxCellLength = 200;

    /// <summary>Writes the document for <paramref name="rows"/>, the rows <paramref name="report"/> shows under <paramref name="filterText"/>.</summary>
    /// <param name="report">The report the rows come from (its totals and any data problem are stated).</param>
    /// <param name="rows">The rows to write, already filtered.</param>
    /// <param name="filterText">The filter in words ("Discipline: Mechanical; status: Open defects").</param>
    /// <param name="applicationVersion">The application version, printed in the header.</param>
    public static string Write(VerificationReport report, IReadOnlyList<VerificationRow> rows, string filterText, string applicationVersion)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(rows);
        var sb = new StringBuilder();
        sb.Append("# TempestOS verification dashboard\n\n");
        sb.Append("Generated ").Append(report.GeneratedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append(" UTC by TempestOS ").Append(Cell(applicationVersion)).Append(".\n\n");
        sb.Append("Filter: ").Append(Cell(string.IsNullOrWhiteSpace(filterText) ? "all rows" : filterText)).Append(".\n\n");
        sb.Append(Summary(report, rows)).Append("\n\n");

        if (report.DataProblem is { Length: > 0 } problem)
            sb.Append("**Note.** ").Append(Cell(problem)).Append(" The columns that come from documents are empty.\n\n");

        if (report.Truncated)
            sb.Append("**Note.** The report holds the first ").Append(report.Rows.Count).Append(" of ").Append(report.TotalRows).Append(" rows.\n\n");

        sb.Append("| Kind | Id | Title | Discipline | Version | Known answers | Unconfirmed | Secondary | Track P re-derivation | Records not entered | Open defects | Manual test |\n");
        sb.Append("|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        var written = 0;

        foreach (var r in rows)
        {
            if (sb.Length > MaxCharacters)
                break;

            sb.Append("| ").Append(r.Kind == VerificationRowKind.Chain ? "Chain" : "Calculator")
              .Append(" | `").Append(Cell(r.Id)).Append("` | ").Append(Cell(r.Title))
              .Append(" | ").Append(Cell(r.Discipline))
              .Append(" | ").Append(r.Version.Length > 0 ? Cell(r.Version) : "—")
              .Append(" | ").Append(Cell(r.KatText))
              .Append(" | ").Append(r.Unconfirmed.ToString(CultureInfo.InvariantCulture))
              .Append(" | ").Append(r.Secondary.ToString(CultureInfo.InvariantCulture))
              .Append(" | ").Append(Cell($"{r.RederivationResult}: {r.RederivationFindings.Text}"))
              .Append(" | ").Append(r.MissingRecordKinds.Count == 0 ? "none" : Cell(string.Join("; ", r.MissingRecordKinds)))
              .Append(" | ").Append(r.OpenDefectCount.ToString(CultureInfo.InvariantCulture))
              .Append(" | ").Append(Cell(r.ManualTest.Label))
              .Append(" |\n");
            written++;
        }

        if (written < rows.Count)
            sb.Append("\nThe document stops after ").Append(written).Append(" of ").Append(rows.Count).Append(" rows (size limit).\n");

        var open = rows.Where(r => r.OpenDefectCount > 0).ToList();
        sb.Append("\n## Open defects\n\n");

        if (open.Count == 0)
        {
            sb.Append("None of the rows above has an open defect.\n");
        }
        else
        {
            sb.Append("A defect is open when `docs/testing/v0.26.0-known-defects.md` does not mark it Fixed: still open, or waiting for the owner's decision against a licensed standard.\n\n");

            foreach (var r in open)
            {
                if (sb.Length > MaxCharacters)
                    break;

                sb.Append("- `").Append(Cell(r.Id)).Append("` (").Append(r.OpenDefectCount.ToString(CultureInfo.InvariantCulture)).Append("): ")
                  .Append(string.Join(", ", r.OpenDefects.Select(d => $"`{Cell(d.Id)}` {Cell(d.Severity)}, {d.StatusLabel.ToLowerInvariant()}"))).Append('\n');
            }
        }

        sb.Append("\n## How to read the columns\n\n");
        sb.Append("- **Known answers.** For a calculator, its known-answer cases and how many passed in the last recorded run (`docs/testing/kat-results.json`); \"not run\" when the results are for another version. For a chain, its issued worked example, which the chain's tests generate and check.\n");
        sb.Append("- **Unconfirmed and Secondary.** Known-answer cases whose reference value could not be confirmed against its source, and cases taken from a secondary source (a solutions manual, a website, course notes). For a chain, the sums over its calculators.\n");
        sb.Append("- **Track P re-derivation.** The result of the independent re-derivation and the findings of Medium or above it raised, fixed or not.\n");
        sb.Append("- **Records not entered.** Record kinds the calculator can draw on that have no record in this database yet.\n");
        sb.Append("- **Manual test.** The outcome of your own test of the calculator, from the guided test runs; \"Not tested\" until one is recorded.\n");
        return sb.ToString();
    }

    private static string Summary(VerificationReport report, IReadOnlyList<VerificationRow> rows)
    {
        var failing = rows.Count(r => r.IsFailing);
        var open = rows.Sum(r => r.OpenDefectCount);
        var openRows = rows.Count(r => r.OpenDefectCount > 0);
        var unconfirmed = rows.Count(r => r.HasUnconfirmedOrSecondary);
        var missing = rows.Count(r => r.HasMissingRecords);
        var notRun = rows.Count(r => r.KatState == KatRunState.NotRun);
        return $"{rows.Count} of {report.Rows.Count} rows shown: {failing} failing, {open} open defect(s) on {openRows} row(s), {unconfirmed} with Unconfirmed or Secondary cases, {missing} with records not entered, {notRun} not run, {rows.Count(r => r.IsClean)} clean.";
    }

    /// <summary>Single-line, table-safe text: line breaks and pipes made harmless, backticks and angle brackets escaped, clipped.</summary>
    internal static string Cell(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var sb = new StringBuilder(Math.Min(text.Length, MaxCellLength));

        foreach (var c in text)
        {
            if (sb.Length >= MaxCellLength)
            {
                sb.Append('…');
                break;
            }

            sb.Append(c switch
            {
                '|' => "\\|",
                '`' => "'",
                '<' => "&lt;",
                '>' => "&gt;",
                '\r' or '\n' or '\t' => " ",
                _ when char.IsControl(c) => " ",
                _ => c.ToString(),
            });
        }

        return sb.ToString().Trim();
    }
}
